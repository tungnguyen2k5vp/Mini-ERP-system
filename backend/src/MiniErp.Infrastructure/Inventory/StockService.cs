using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Inventory;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Inventory;

public sealed class StockService(ErpDbContext db, TimeProvider clock)
{
    public async Task<Page<StockDocumentRow>> DocumentsAsync(StockKind? kind, StockStatus? status, Guid? warehouseId,
        int page, int size, CancellationToken ct)
    {
        var query = db.StockDocuments.AsNoTracking().Where(x =>
            (kind == null || x.Kind == kind) && (status == null || x.Status == status) &&
            (warehouseId == null || x.WarehouseId == warehouseId || x.DestinationId == warehouseId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).Select(RowProjection).ToListAsync(ct);
        return new Page<StockDocumentRow>(items, page, size, total);
    }

    public async Task<StockDocumentResponse> DetailAsync(int id, CancellationToken ct)
    {
        await using var transaction = await db.BeginDocumentReadAsync(ct);
        var row = await db.StockDocuments.FromSqlInterpolated($"SELECT * FROM inventory.Documents WITH (HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().Select(RowProjection).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("stock_document_missing", "Không tìm thấy chứng từ kho.");
        var detail = await db.StockDocuments.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Note,
            x.DestinationId,
            Lines = x.Lines.OrderBy(l => l.Sku).Select(l =>
                new StockLineResponse(l.ProductId, l.Sku, l.ProductName, l.Quantity, l.UnitCost)).ToArray()
        }).SingleAsync(ct);
        var result = new StockDocumentResponse(row, detail.Note, detail.DestinationId, detail.Lines,
            await db.DocumentHistoryAsync("StockDocument", id, ct));
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<StockDocumentResponse> CreateAsync(StockDocumentInput input, RequestActor actor, CancellationToken ct)
    {
        Validate(input);
        var note = CatalogRules.OptionalText(input.Note);
        await using var transaction = await db.BeginDocumentWriteAsync(ct);
        await db.LockDocumentRequestAsync("stock-create-" + input.RequestId, ct);
        var previous = await db.StockDocuments.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (previous is not null)
        {
            if ((previous.CreatePayloadHash ?? Fingerprint(previous)) != Fingerprint(input))
                throw ErpException.Conflict("request_id_conflict", "Mã yêu cầu đã được dùng cho chứng từ khác.");
            return await DetailAsync(previous.Id, ct);
        }

        var productIds = input.Lines.Select(x => x.ProductId).ToArray();
        var (products, _) = await ActiveRecordsAsync(input.WarehouseId, input.DestinationId, productIds, ct);
        var countVersions = input.Kind == StockKind.Count
            ? await db.StockBalances.Where(x => x.WarehouseId == input.WarehouseId && productIds.Contains(x.ProductId))
                .ToDictionaryAsync(x => x.ProductId, x => x.Version, ct)
            : [];
        var doc = new StockDocument
        {
            RequestId = input.RequestId,
            Kind = input.Kind,
            WarehouseId = input.WarehouseId,
            DestinationId = input.DestinationId,
            Note = note,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Lines = input.Lines.Select(line => new StockLine
            {
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                Sku = products[line.ProductId].Sku,
                ProductName = products[line.ProductId].Name,
                CountVersion = countVersions.GetValueOrDefault(line.ProductId)
            }).ToList()
        };
        doc.FreezeCreatePayload(Fingerprint(input));
        db.StockDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        Audit(doc, actor, "CreateStockDocument");
        await db.SaveChangesAsync(ct);
        var detail = await DetailAsync(doc.Id, ct);
        await transaction.CommitAsync(ct);
        return detail;
    }

    public async Task<StockDocumentResponse> PostAsync(int id, RequestActor actor, CancellationToken ct, bool linked = false, byte[]? version = null)
    {
        await using var transaction = linked ? null : await db.BeginDocumentWriteAsync(ct);
        var doc = await LoadAsync(id, ct);
        if (!linked && doc.Status == StockStatus.Posted && version is { Length: 8 })
        {
            var token = Convert.ToBase64String(version);
            var originalToken = await db.AuditEntries.Where(x => x.Entity == "StockDocument" &&
                    x.EntityId == id.ToString() && x.Action == "PostStockDocument" &&
                    x.UserId == actor.UserId && x.Result == "Succeeded")
                .Select(x => x.InputVersion).SingleOrDefaultAsync(ct);
            if (originalToken == token)
                return await DetailAsync(id, ct);
            throw ErpException.Conflict("post_replay_conflict", "Yêu cầu ghi sổ khác với lần ghi sổ trước.");
        }
        if (!linked)
        {
            if (version is not null) RequireVersion(doc, version);
            else if (doc.IsEdited) throw ErpException.Conflict("version_required", "Phiếu đã sửa cần phiên bản trước khi ghi sổ.");
        }
        if (doc.Status == StockStatus.Posted)
            return await DetailAsync(id, ct);
        var stamp = clock.GetUtcNow().UtcDateTime;
        doc.Post(stamp);
        var productIds = doc.Lines.Select(x => x.ProductId).ToArray();
        var (products, warehouseIds) = await ActiveRecordsAsync(doc.WarehouseId, doc.DestinationId, productIds, ct);
        var balances = await LoadBalancesAsync(warehouseIds, productIds, ct);

        foreach (var line in doc.Lines.OrderBy(x => x.ProductId))
        {
            var source = Balance(doc.WarehouseId, line.ProductId, balances);
            if (doc.Kind == StockKind.Count &&
                !source.Version.AsSpan().SequenceEqual(line.CountVersion ?? []))
            {
                throw ErpException.Conflict("stale_stock_count", "Kho đã biến động sau khi lập phiếu. Hãy kiểm kê lại.");
            }
            var delta = doc.Kind switch
            {
                StockKind.Issue or StockKind.Transfer => -line.Quantity,
                StockKind.Count => line.Quantity - source.OnHand,
                _ => line.Quantity
            };
            var defaultCost = source.OnHand > 0 ? source.AverageCost : products[line.ProductId].PurchasePrice;
            var cost = Apply(doc, source, delta, line.UnitCost ?? defaultCost, stamp);
            if (doc.DestinationId is { } dest)
            {
                Apply(doc, Balance(dest, line.ProductId, balances), line.Quantity, cost, stamp);
            }
        }

        Audit(doc, actor, "PostStockDocument", version is null ? null : Convert.ToBase64String(version));
        await db.SaveChangesAsync(ct);
        var detail = await DetailAsync(id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return detail;
    }

    public async Task<StockDocumentResponse> EditAsync(int id, StockDocumentEditInput input,
        RequestActor actor, bool canSetCost, CancellationToken ct)
    {
        await using var transaction = await db.BeginDocumentWriteAsync(ct);
        var doc = await LoadAsync(id, ct);
        RequireVersion(doc, input.Version);
        if (doc.Status != StockStatus.Draft)
            throw ErpException.Conflict("invalid_status", "Chỉ được sửa phiếu nháp.");
        if (doc.Kind == StockKind.Count && !input.Recount)
            throw ErpException.Conflict("recount_required", "Hãy kiểm kê lại và xác nhận số đếm mới trước khi sửa.");
        if (input.Lines is not { Length: >= 1 and <= 50 } || input.Lines.Any(x => x is null))
            throw ErpException.Invalid("invalid_document", "Chọn từ 1 đến 50 dòng sản phẩm.");
        if (!canSetCost && input.Lines.Any(x => x.UnitCost is not null))
            throw ErpException.Forbidden("financial_fields_forbidden", "Bạn không có quyền sửa giá vốn.");
        var ids = input.Lines.Select(x => x.ProductId).ToArray();
        var (products, _) = await ActiveRecordsAsync(input.WarehouseId, input.DestinationId, ids, ct);
        var lines = input.Lines.Select(x => x with
        {
            UnitCost = x.UnitCost ?? doc.Lines.FirstOrDefault(l => l.ProductId == x.ProductId)?.UnitCost
                ?? (doc.Kind == StockKind.Receipt && !canSetCost ? products[x.ProductId].PurchasePrice : null)
        }).ToArray();
        Validate(new StockDocumentInput(doc.RequestId, doc.Kind, input.WarehouseId, input.DestinationId, input.Note, lines));
        var counts = doc.Kind == StockKind.Count
            ? await db.StockBalances.Where(x => x.WarehouseId == input.WarehouseId && ids.Contains(x.ProductId))
                .ToDictionaryAsync(x => x.ProductId, x => x.Version, ct) : [];
        doc.FreezeCreatePayload(Fingerprint(doc));
        doc.Edit(input.WarehouseId, input.DestinationId, CatalogRules.OptionalText(input.Note));
        db.Entry(doc).Property(x => x.IsEdited).IsModified = true;
        // Delete first inside the transaction: retained SKUs share the composite key.
        db.Set<StockLine>().RemoveRange(doc.Lines);
        await db.SaveChangesAsync(ct);
        doc.Lines.Clear();
        doc.Lines.AddRange(lines.Select(x => new StockLine
        {
            ProductId = x.ProductId, Quantity = x.Quantity, UnitCost = x.UnitCost,
            Sku = products[x.ProductId].Sku, ProductName = products[x.ProductId].Name,
            CountVersion = counts.GetValueOrDefault(x.ProductId)
        }));
        Audit(doc, actor, "EditStockDocument", Convert.ToBase64String(input.Version));
        await db.SaveChangesAsync(ct);
        var detail = await DetailAsync(id, ct);
        await transaction.CommitAsync(ct);
        return detail;
    }

    public async Task<StockDocumentResponse> CancelAsync(int id, StockCancelInput input, RequestActor actor, CancellationToken ct)
    {
        var reason = CatalogRules.OptionalText(input.Reason);
        if (reason is null || reason.Length > 300 || input.Version is not { Length: 8 })
            throw ErpException.Invalid("invalid_cancellation", "Nhập phiên bản và lý do hủy tối đa 300 ký tự.");
        await using var transaction = await db.BeginDocumentWriteAsync(ct);
        var doc = await LoadAsync(id, ct);
        if (doc.Status == StockStatus.Cancelled)
        {
            var prior = await db.AuditEntries.AsNoTracking().Where(x => x.Entity == "StockDocument" &&
                x.EntityId == id.ToString() && x.Action == "CancelStockDocument").OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (prior?.Reason != reason || prior.InputVersion != Convert.ToBase64String(input.Version) || prior.UserId != actor.UserId)
                throw ErpException.Conflict("cancellation_conflict", "Yêu cầu hủy khác với lần hủy trước.");
            return await DetailAsync(id, ct);
        }
        RequireVersion(doc, input.Version);
        doc.Cancel();
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, "CancelStockDocument",
            "StockDocument", id.ToString(), after: doc.Status.ToString(), reason: reason, inputVersion: Convert.ToBase64String(input.Version)));
        await db.SaveChangesAsync(ct);
        var detail = await DetailAsync(id, ct);
        await transaction.CommitAsync(ct);
        return detail;
    }

    private static void RequireVersion(StockDocument doc, byte[]? version)
    {
        if (version is not { Length: 8 }) throw ErpException.Invalid("invalid_version", "Phiên bản không hợp lệ.");
        if (!doc.Version.AsSpan().SequenceEqual(version))
            throw ErpException.Conflict("stale_document", "Phiếu đã thay đổi. Hãy tải lại.");
    }

    private static string Fingerprint(StockDocument doc) => Fingerprint(new StockDocumentInput(doc.RequestId,
        doc.Kind, doc.WarehouseId, doc.DestinationId, doc.Note,
        doc.Lines.Select(x => new StockLineInput(x.ProductId, x.Quantity, x.UnitCost)).ToArray()));

    private static string Fingerprint(StockDocumentInput input) => DocumentFingerprint.Compute(new
    {
        input.Kind, input.WarehouseId, input.DestinationId, Note = CatalogRules.OptionalText(input.Note),
        Lines = input.Lines.OrderBy(x => x.ProductId).Select(x => new { x.ProductId, x.Quantity, x.UnitCost }).ToArray()
    });

    public async Task<StockDocumentResponse> ReverseAsync(int id, RequestActor actor, CancellationToken ct, bool linked = false)
    {
        await using var transaction = linked ? null : await db.BeginDocumentWriteAsync(ct);
        var original = await LoadAsync(id, ct);
        // Read the source first: an in-flight order posting may not have inserted its source link yet.
        if (!linked && await db.OrderFulfillments.AnyAsync(x => x.StockDocumentId == id, ct))
            throw ErpException.Conflict("linked_document", "Đảo phiếu từ đơn hàng nguồn để đối soát công nợ.");
        if (original.Status == StockStatus.Reversed)
        {
            var existing = await db.StockDocuments.Where(x => x.ReversesId == id).Select(x => x.Id).SingleAsync(ct);
            return await DetailAsync(existing, ct);
        }
        original.MarkReversed();
        var prior = await db.StockMovements.AsNoTracking().Where(x => x.DocumentId == id).ToArrayAsync(ct);
        var warehouseIds = prior.Select(x => x.WarehouseId).Distinct().ToArray();
        var productIds = prior.Select(x => x.ProductId).Distinct().ToArray();

        // Read balances before checking the ledger. A concurrent write after either read invalidates the balance rowversion.
        var balances = await LoadBalancesAsync(warehouseIds, productIds, ct);
        var latest = await db.StockMovements.Where(x => warehouseIds.Contains(x.WarehouseId) && productIds.Contains(x.ProductId))
            .GroupBy(x => new { x.WarehouseId, x.ProductId })
            .Select(group => new { group.Key.WarehouseId, group.Key.ProductId, Last = group.Max(x => x.Id) })
            .ToDictionaryAsync(x => (x.WarehouseId, x.ProductId), x => x.Last, ct);
        if (prior.Any(x => latest[(x.WarehouseId, x.ProductId)] != x.Id))
        {
            throw ErpException.Conflict("later_stock_movement", "Có biến động kho mới hơn. Hãy điều chỉnh tồn kho hiện tại.");
        }

        var stamp = clock.GetUtcNow().UtcDateTime;
        var reversal = new StockDocument
        {
            RequestId = Guid.NewGuid(),
            Kind = StockKind.Reversal,
            WarehouseId = original.WarehouseId,
            DestinationId = original.DestinationId,
            ReversesId = original.Id,
            CreatedAtUtc = stamp,
            Note = "Đảo chứng từ " + DocumentNumber(original.Kind, original.Id),
            Lines = original.Lines.Select(x => new StockLine
            {
                ProductId = x.ProductId,
                Sku = x.Sku,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            }).ToList()
        };
        reversal.Post(stamp);
        db.StockDocuments.Add(reversal);
        foreach (var entry in prior)
        {
            var balance = balances[(entry.WarehouseId, entry.ProductId)];
            var previousCost = balance.AverageCost;
            balance.Reverse(entry.QuantityDelta, entry.PreviousAverageCost);
            RecordMovement(reversal, balance, -entry.QuantityDelta, entry.UnitCost, previousCost, stamp);
        }
        Audit(original, actor, "ReverseStockDocument");
        await db.SaveChangesAsync(ct);
        var detail = await DetailAsync(reversal.Id, ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return detail;
    }

    public async Task<Page<StockMovementRow>> HistoryAsync(Guid? warehouseId, Guid? productId, int page, int size, CancellationToken ct)
    {
        var query = from entry in db.StockMovements.AsNoTracking()
                    join warehouse in db.Lookups on entry.WarehouseId equals warehouse.Id
                    join product in db.Products on entry.ProductId equals product.Id
                    where (warehouseId == null || entry.WarehouseId == warehouseId) &&
                        (productId == null || entry.ProductId == productId)
                    select new { entry, warehouse.Name, product.Sku };
        var total = await query.CountAsync(ct);
        var data = await query.OrderByDescending(x => x.entry.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new StockMovementRow(x.entry.Id, x.entry.DocumentId, DocumentNumber(x.entry.Document.Kind, x.entry.DocumentId),
                x.entry.WarehouseId, x.Name, x.entry.ProductId, x.Sku, x.entry.QuantityDelta, x.entry.UnitCost,
                x.entry.BalanceAfter, new DateTimeOffset(x.entry.PostedAtUtc, TimeSpan.Zero))).ToListAsync(ct);
        return new Page<StockMovementRow>(data, page, size, total);
    }

    public Task<StockProductOption[]> ProductOptionsAsync(CancellationToken ct) =>
        db.Products.Where(x => x.IsActive).OrderBy(x => x.Sku)
            .Select(x => new StockProductOption(x.Id, x.Sku, x.Name, x.PurchasePrice, x.SalePrice)).ToArrayAsync(ct);

    private async Task<StockDocument> LoadAsync(int id, CancellationToken ct) =>
        await db.StockDocuments.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw ErpException.Missing("stock_document_missing", "Không tìm thấy chứng từ kho.");

    private async Task<(Dictionary<Guid, Product> Products, Guid[] Warehouses)> ActiveRecordsAsync(
        Guid origin, Guid? destination, Guid[] productIds, CancellationToken ct)
    {
        Guid[] warehouseIds = destination is { } other ? [origin, other] : [origin];
        var warehouseCount = await db.Lookups.CountAsync(x => x.Kind == LookupKind.Warehouse &&
            x.IsActive && warehouseIds.Contains(x.Id), ct);
        var products = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, ct);
        if (warehouseCount != warehouseIds.Length || products.Count != productIds.Length)
        {
            throw ErpException.Invalid("inactive_stock_reference", "Chọn kho và sản phẩm đang sử dụng.");
        }
        return (products, warehouseIds);
    }

    private Task<Dictionary<(Guid Warehouse, Guid Product), StockBalance>> LoadBalancesAsync(
        Guid[] warehouses, Guid[] products, CancellationToken ct) =>
        db.StockBalances.Where(x => warehouses.Contains(x.WarehouseId) && products.Contains(x.ProductId))
            .ToDictionaryAsync(x => (x.WarehouseId, x.ProductId), ct);

    private StockBalance Balance(Guid warehouse, Guid product, Dictionary<(Guid, Guid), StockBalance> balances)
    {
        if (!balances.TryGetValue((warehouse, product), out var balance))
        {
            balance = new StockBalance { WarehouseId = warehouse, ProductId = product };
            db.StockBalances.Add(balance);
            balances.Add((warehouse, product), balance);
        }
        return balance;
    }

    private decimal Apply(StockDocument doc, StockBalance balance, decimal delta, decimal receiptCost, DateTime stamp)
    {
        if (delta == 0) return 0;
        var previousCost = balance.AverageCost;
        var unitCost = receiptCost;
        if (delta > 0) balance.Receive(delta, receiptCost);
        else unitCost = balance.Issue(-delta);
        RecordMovement(doc, balance, delta, unitCost, previousCost, stamp);
        return unitCost;
    }

    private void RecordMovement(StockDocument doc, StockBalance balance, decimal delta, decimal unitCost, decimal previousCost,
        DateTime stamp) => db.StockMovements.Add(new StockMovement
        {
            Document = doc,
            WarehouseId = balance.WarehouseId,
            ProductId = balance.ProductId,
            QuantityDelta = delta,
            UnitCost = unitCost,
            PreviousAverageCost = previousCost,
            BalanceAfter = balance.OnHand,
            PostedAtUtc = stamp
        });

    private void Audit(StockDocument doc, RequestActor actor, string action, string? inputVersion = null) =>
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, action,
            "StockDocument", doc.Id.ToString(), after: doc.Status.ToString(), inputVersion: inputVersion));

    private static void Validate(StockDocumentInput input)
    {
        if (input.RequestId == Guid.Empty || input.WarehouseId == Guid.Empty ||
            input.Note?.Length > 300 || input.Lines is not { Length: >= 1 and <= 50 } ||
            input.Lines.Any(x => x is null || x.ProductId == Guid.Empty))
        {
            throw ErpException.Invalid("invalid_document", "Chọn kho và từ 1 đến 50 dòng sản phẩm.");
        }
        if (!Enum.IsDefined(input.Kind) || input.Kind == StockKind.Reversal ||
            (input.Kind != StockKind.Transfer && input.DestinationId is not null) ||
            (input.Kind == StockKind.Transfer && (input.DestinationId is null || input.DestinationId == input.WarehouseId)))
        {
            throw ErpException.Invalid("invalid_stock_kind", "Loại chứng từ hoặc kho đích không hợp lệ.");
        }
        if (input.Lines.Select(x => x.ProductId).Distinct().Count() != input.Lines.Length)
        {
            throw ErpException.Invalid("duplicate_stock_line", "Mỗi sản phẩm chỉ xuất hiện một lần trên phiếu.");
        }
        if (input.Kind is StockKind.Adjustment or StockKind.Count && string.IsNullOrWhiteSpace(input.Note))
        {
            throw ErpException.Invalid("missing_reason", "Nhập lý do điều chỉnh hoặc kiểm kê.");
        }
        foreach (var line in input.Lines)
        {
            if (input.Kind == StockKind.Count) CatalogRules.NonNegative(line.Quantity);
            else StockBalance.RequirePositive(input.Kind == StockKind.Adjustment ? Math.Abs(line.Quantity) : line.Quantity);
            if (input.Kind == StockKind.Receipt && line.UnitCost is null)
            {
                throw ErpException.Invalid("unit_cost_required", "Nhập giá vốn cho từng dòng hàng nhập.");
            }
            if (line.UnitCost is { } cost)
            {
                if (input.Kind is StockKind.Issue or StockKind.Transfer)
                {
                    throw ErpException.Invalid("unit_cost_computed", "Giá vốn xuất kho được xác định khi ghi sổ.");
                }
                CatalogRules.NonNegative(cost);
            }
        }
    }

    private static string DocumentNumber(StockKind kind, int id)
    {
        var prefix = kind switch
        {
            StockKind.Receipt => "PN",
            StockKind.Issue => "PX",
            StockKind.Transfer => "CK",
            StockKind.Adjustment => "DC",
            StockKind.Count => "KK",
            _ => "DA"
        };
        return $"{prefix}-{id:000000}";
    }

    private static readonly Expression<Func<StockDocument, StockDocumentRow>> RowProjection = row =>
        new StockDocumentRow(row.Id, DocumentNumber(row.Kind, row.Id), row.Kind, row.Status,
            row.WarehouseId, row.Warehouse.Name, row.Destination == null ? null : row.Destination.Name,
            new DateTimeOffset(row.CreatedAtUtc, TimeSpan.Zero),
            row.PostedAtUtc == null ? null : new DateTimeOffset(row.PostedAtUtc.Value, TimeSpan.Zero), row.ReversesId, row.Version);
}
