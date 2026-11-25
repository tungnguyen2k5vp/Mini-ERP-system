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
        var row = await db.StockDocuments.Where(x => x.Id == id).Select(RowProjection).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("stock_document_missing", "Không tìm thấy chứng từ kho.");
        var detail = await db.StockDocuments.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Note, x.DestinationId, Lines = x.Lines.OrderBy(l => l.Sku).Select(l =>
                new StockLineResponse(l.ProductId, l.Sku, l.ProductName, l.Quantity, l.UnitCost)).ToArray()
        }).SingleAsync(ct);
        return new StockDocumentResponse(row, detail.Note, detail.DestinationId, detail.Lines);
    }

    public async Task<StockDocumentResponse> CreateAsync(StockDocumentInput input, RequestActor actor, CancellationToken ct)
    {
        Validate(input);
        var note = CatalogRules.OptionalText(input.Note);
        var previous = await db.StockDocuments.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (previous is not null)
        {
            if (previous.Kind != input.Kind || previous.WarehouseId != input.WarehouseId ||
                previous.DestinationId != input.DestinationId || previous.Note != note || previous.Lines.Count != input.Lines.Length ||
                !input.Lines.All(line => previous.Lines.Any(x => x.ProductId == line.ProductId &&
                    x.Quantity == line.Quantity && x.UnitCost == line.UnitCost)))
            {
                throw ErpException.Conflict("request_id_conflict", "Mã yêu cầu đã được dùng cho chứng từ khác.");
            }
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
            RequestId = input.RequestId, Kind = input.Kind, WarehouseId = input.WarehouseId, DestinationId = input.DestinationId,
            Note = note, CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            Lines = input.Lines.Select(line => new StockLine
            {
                ProductId = line.ProductId, Quantity = line.Quantity, UnitCost = line.UnitCost,
                Sku = products[line.ProductId].Sku, ProductName = products[line.ProductId].Name,
                CountVersion = countVersions.GetValueOrDefault(line.ProductId)
            }).ToList()
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.StockDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        Audit(doc, actor, "CreateStockDocument");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(doc.Id, ct);
    }

    public async Task<StockDocumentResponse> PostAsync(int id, RequestActor actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var doc = await LoadAsync(id, ct);
        if (doc.Status == StockStatus.Posted)
        {
            return await DetailAsync(id, ct);
        }
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

        Audit(doc, actor, "PostStockDocument");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(id, ct);
    }

    public async Task<StockDocumentResponse> CancelAsync(int id, RequestActor actor, CancellationToken ct)
    {
        var doc = await LoadAsync(id, ct);
        if (doc.Status == StockStatus.Cancelled) return await DetailAsync(id, ct);
        doc.Cancel();
        Audit(doc, actor, "CancelStockDocument");
        await db.SaveChangesAsync(ct);
        return await DetailAsync(id, ct);
    }

    public async Task<StockDocumentResponse> ReverseAsync(int id, RequestActor actor, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var original = await LoadAsync(id, ct);
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
            RequestId = Guid.NewGuid(), Kind = StockKind.Reversal, WarehouseId = original.WarehouseId,
            DestinationId = original.DestinationId, ReversesId = original.Id, CreatedAtUtc = stamp,
            Note = "Đảo chứng từ " + DocumentNumber(original.Kind, original.Id),
            Lines = original.Lines.Select(x => new StockLine
            {
                ProductId = x.ProductId, Sku = x.Sku, ProductName = x.ProductName, Quantity = x.Quantity, UnitCost = x.UnitCost
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
        await transaction.CommitAsync(ct);
        return await DetailAsync(reversal.Id, ct);
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
            .Select(x => new StockProductOption(x.Id, x.Sku, x.Name, x.PurchasePrice)).ToArrayAsync(ct);

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
            Document = doc, WarehouseId = balance.WarehouseId, ProductId = balance.ProductId, QuantityDelta = delta,
            UnitCost = unitCost, PreviousAverageCost = previousCost, BalanceAfter = balance.OnHand, PostedAtUtc = stamp
        });

    private void Audit(StockDocument doc, RequestActor actor, string action) =>
        db.AuditEntries.Add(AuditEntry.Create(actor, clock.GetUtcNow().UtcDateTime, action,
            "StockDocument", doc.Id.ToString(), after: doc.Status.ToString()));

    private static void Validate(StockDocumentInput input)
    {
        if (input.RequestId == Guid.Empty || input.WarehouseId == Guid.Empty ||
            input.Lines is not { Length: >= 1 and <= 50 } || input.Lines.Any(x => x is null || x.ProductId == Guid.Empty))
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
            StockKind.Receipt => "PN", StockKind.Issue => "PX", StockKind.Transfer => "CK",
            StockKind.Adjustment => "DC", StockKind.Count => "KK", _ => "DA"
        };
        return $"{prefix}-{id:000000}";
    }

    private static readonly Expression<Func<StockDocument, StockDocumentRow>> RowProjection = row =>
        new StockDocumentRow(row.Id, DocumentNumber(row.Kind, row.Id), row.Kind, row.Status,
            row.WarehouseId, row.Warehouse.Name, row.Destination == null ? null : row.Destination.Name,
            new DateTimeOffset(row.CreatedAtUtc, TimeSpan.Zero),
            row.PostedAtUtc == null ? null : new DateTimeOffset(row.PostedAtUtc.Value, TimeSpan.Zero), row.ReversesId);
}
