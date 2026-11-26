using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;
namespace MiniErp.Infrastructure.Orders;

public sealed class OrderService(ErpDbContext db, StockService stock, IFinancePosting finance, TimeProvider clock)
{
    public static string Number(OrderKind kind, int id) => $"{(kind == OrderKind.Purchase ? "PO" : "SO")}-{id:000000}";
    public async Task<OrderKind> KindAsync(int id, CancellationToken ct) => await db.Orders.Where(x => x.Id == id).Select(x => (OrderKind?)x.Kind)
        .SingleOrDefaultAsync(ct) ?? throw ErpException.Missing("order_missing", "Không tìm thấy đơn.");
    public async Task<OrderOptions> OptionsAsync(OrderKind kind, CancellationToken ct)
    {
        var partnerKind = kind == OrderKind.Purchase ? PartnerKind.Supplier : PartnerKind.Customer;
        var partners = await db.Partners.Where(x => x.Kind == partnerKind).OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new OrderOption(x.Id, x.Name, x.IsActive)).ToArrayAsync(ct);
        var warehouses = await db.Lookups.Where(x => x.Kind == LookupKind.Warehouse && x.IsActive).OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new OrderOption(x.Id, x.Name, x.IsActive)).ToArrayAsync(ct);
        var products = await db.Products.Where(x => x.IsActive).OrderBy(x => x.Sku).Select(x => new OrderProductOption(x.Id, x.Sku, x.Name, x.PurchasePrice, x.SalePrice)).ToArrayAsync(ct);
        return new(partners, warehouses, products);
    }
    public async Task<Page<OrderRow>> ListAsync(OrderKind kind, Guid? partnerId, int page, int size, CancellationToken ct,
        string? search = null, OrderStatus? status = null, string? sort = null, bool descending = true)
    {
        var query = db.Orders.AsNoTracking().Where(x => x.Kind == kind && (partnerId == null || x.PartnerId == partnerId));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            var numberText = text.Contains('-') ? text[(text.LastIndexOf('-') + 1)..] : text;
            int.TryParse(numberText, out var number);
            query = query.Where(x => x.Partner.Name.Contains(text) || x.Id == number);
        }
        var total = await query.CountAsync(ct);
        IOrderedQueryable<TradeOrder> ordered = sort switch
        {
            "status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "total" => descending ? query.OrderByDescending(x => x.Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2))) : query.OrderBy(x => x.Lines.Sum(l => Math.Round(l.Quantity * l.UnitPrice, 2))),
            "createdAtUtc" => descending ? query.OrderByDescending(x => x.CreatedAtUtc) : query.OrderBy(x => x.CreatedAtUtc),
            _ => descending ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)
        };
        var data = await ordered.ThenBy(x => x.Id).Include(x => x.Partner).Include(x => x.Warehouse).Include(x => x.Lines)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(data.Select(Row).ToArray(), page, size, total);
    }
    public async Task<OrderDetail> DetailAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, ct);
        var fulfillments = await db.OrderFulfillments.Where(x => x.OrderId == id).OrderByDescending(x => x.Id)
            .Select(x => new FulfillmentResponse(x.Id, x.StockDocumentId, x.IsReturn, x.ReversesId, x.CreatedAtUtc, x.Amount)).ToArrayAsync(ct);
        return new(Row(order), order.Note, order.Lines.Select(x => new OrderLineResponse(x.ProductId, x.Sku, x.ProductName,
            x.Quantity, x.UnitPrice, x.FulfilledQuantity, x.ReturnedQuantity, x.ReservedQuantity)).ToArray(), fulfillments);
    }
    public async Task<OrderDetail> CreateAsync(OrderInput input, RequestActor actor, CancellationToken ct)
    {
        if (!Enum.IsDefined(input.Kind) || input.RequestId == Guid.Empty || input.Lines is not { Length: >= 1 and <= 50 } ||
            input.Lines.Any(x => x is null || x.ProductId == Guid.Empty) || input.Lines.Select(x => x.ProductId).Distinct().Count() != input.Lines.Length)
            throw ErpException.Invalid("invalid_order", "Chọn từ 1 đến 50 sản phẩm khác nhau.");
        var note = CatalogRules.OptionalText(input.Note);
        if (note?.Length > 300) throw ErpException.Invalid("invalid_note", "Ghi chú tối đa 300 ký tự.");
        foreach (var line in input.Lines) { StockBalance.RequirePositive(line.Quantity); CatalogRules.NonNegative(line.UnitPrice); }
        if (input.Lines.Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2, MidpointRounding.AwayFromZero)) > 999_999_999_999m)
            throw ErpException.Invalid("order_total_exceeded", "Tổng tiền đơn vượt giới hạn cho phép.");
        var prior = await db.Orders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (prior is not null)
        {
            if (prior.Kind != input.Kind || prior.PartnerId != input.PartnerId || prior.WarehouseId != input.WarehouseId || prior.Note != note ||
                prior.Lines.Count != input.Lines.Length || !input.Lines.All(l => prior.Lines.Any(x => x.ProductId == l.ProductId && x.Quantity == l.Quantity && x.UnitPrice == l.UnitPrice)))
                throw ErpException.Conflict("request_id_conflict", "Mã yêu cầu đã dùng cho dữ liệu khác.");
            return await DetailAsync(prior.Id, ct);
        }
        var partnerKind = input.Kind == OrderKind.Purchase ? PartnerKind.Supplier : PartnerKind.Customer;
        var ids = input.Lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (products.Count != ids.Length || !await db.Partners.AnyAsync(x => x.Id == input.PartnerId && x.Kind == partnerKind && x.IsActive, ct) ||
            !await db.Lookups.AnyAsync(x => x.Id == input.WarehouseId && x.Kind == LookupKind.Warehouse && x.IsActive, ct))
            throw ErpException.Invalid("invalid_reference", "Chọn đối tác, kho và sản phẩm đang sử dụng.");
        var order = new TradeOrder
        {
            RequestId = input.RequestId,
            Kind = input.Kind,
            PartnerId = input.PartnerId,
            WarehouseId = input.WarehouseId,
            Note = note,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            Lines = input.Lines.Select(x => new OrderLine
            {
                ProductId = x.ProductId,
                Sku = products[x.ProductId].Sku,
                ProductName = products[x.ProductId].Name,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice
            }).ToList()
        };
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        Audit(order, actor, "CreateOrder");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await DetailAsync(order.Id, ct);
    }
    public async Task<OrderDetail> TransitionAsync(int id, string action, OrderVersion input, RequestActor actor, CancellationToken ct)
    {
        if (action is not ("submit" or "approve" or "cancel"))
            throw ErpException.Invalid("invalid_order_transition", "Thao tác chuyển trạng thái không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrderAsync(id, ct);
        var order = await LoadAsync(id, ct);
        if (action == "cancel" && order.Status == OrderStatus.Cancelled) return await DetailAsync(id, ct);
        if (input.Version != Convert.ToBase64String(order.Version)) throw ErpException.Conflict("stale_version", "Đơn đã thay đổi. Hãy tải lại.");
        if (action == "submit") order.Submit();
        else if (action == "approve")
        {
            order.Approve();
            if (order.Kind == OrderKind.Sales)
                foreach (var line in order.Lines.OrderBy(x => x.ProductId))
                {
                    var balance = await BalanceAsync(order.WarehouseId, line.ProductId, ct);
                    balance.Reserve(line.Quantity); line.ReservedQuantity = line.Quantity;
                }
        }
        else if (action == "cancel")
        {
            order.Cancel();
            foreach (var line in order.Lines.Where(x => x.ReservedQuantity > 0).OrderBy(x => x.ProductId))
            {
                (await BalanceAsync(order.WarehouseId, line.ProductId, ct)).Release(line.ReservedQuantity);
                line.ReservedQuantity = 0;
            }
        }
        order.UpdatedAtUtc = Now;
        db.Entry(order).Property(x => x.UpdatedAtUtc).IsModified = true;
        Audit(order, actor, action + "Order");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await DetailAsync(id, ct);
    }
    public async Task<OrderDetail> FulfillAsync(int id, FulfillmentInput input, RequestActor actor, CancellationToken ct)
    {
        if (input.RequestId == Guid.Empty || input.Lines is not { Length: >= 1 and <= 50 } || input.Lines.Any(x => x is null) ||
            input.Lines.Select(x => x.ProductId).Distinct().Count() != input.Lines.Length)
            throw ErpException.Invalid("invalid_fulfillment", "Chọn các dòng hàng khác nhau.");
        foreach (var line in input.Lines) StockBalance.RequirePositive(line.Quantity);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrderAsync(id, ct);
        var order = await LoadAsync(id, ct);
        var previous = await db.OrderFulfillments.SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (previous is not null)
        {
            var postedLines = await db.StockDocuments.Where(x => x.Id == previous.StockDocumentId).SelectMany(x => x.Lines).ToArrayAsync(ct);
            if (previous.OrderId != id || previous.IsReturn != input.IsReturn || postedLines.Length != input.Lines.Length ||
                !input.Lines.All(l => postedLines.Any(x => x.ProductId == l.ProductId && x.Quantity == l.Quantity)))
                throw ErpException.Conflict("request_id_conflict", "Mã yêu cầu đã dùng cho phiếu khác.");
            return await DetailAsync(id, ct);
        }
        order.RequireFulfillable();
        if (!input.IsReturn && order.Status is OrderStatus.Fulfilled or OrderStatus.Cancelled)
            throw ErpException.Conflict("invalid_status", "Đơn đã đóng.");
        var incoming = order.Kind == OrderKind.Purchase != input.IsReturn;
        var doc = new StockDocument
        {
            RequestId = input.RequestId,
            Kind = incoming ? StockKind.Receipt : StockKind.Issue,
            WarehouseId = order.WarehouseId,
            Note = (input.IsReturn ? "Trả hàng " : "Thực hiện ") + Number(order.Kind, id),
            CreatedAtUtc = Now
        };
        decimal amount = 0;
        foreach (var item in input.Lines.OrderBy(x => x.ProductId))
        {
            var line = order.Lines.SingleOrDefault(x => x.ProductId == item.ProductId)
                ?? throw ErpException.Invalid("invalid_order_line", "Sản phẩm không thuộc đơn.");
            if (item.Quantity > (input.IsReturn ? line.FulfilledQuantity - line.ReturnedQuantity : line.Quantity - line.FulfilledQuantity))
                throw ErpException.Conflict("quantity_exceeded", "Số lượng vượt phần còn lại của đơn.");
            decimal? cost = order.Kind == OrderKind.Purchase ? line.UnitPrice : null;
            var beforeQuantity = input.IsReturn ? line.ReturnedQuantity : line.FulfilledQuantity;
            if (input.IsReturn)
            {
                line.ReturnedQuantity += item.Quantity;
                if (order.Kind == OrderKind.Sales)
                {
                    var movements = await (from f in db.OrderFulfillments
                                           join m in db.StockMovements on f.StockDocumentId equals m.DocumentId
                                           where f.OrderId == id && (m.Document.Status == StockStatus.Posted || m.Document.Status == StockStatus.Reversed) && m.ProductId == item.ProductId
                                           select new { m.QuantityDelta, m.UnitCost }).ToArrayAsync(ct);
                    var outstandingQuantity = movements.Sum(x => -x.QuantityDelta);
                    if (outstandingQuantity <= 0)
                        throw ErpException.Conflict("quantity_exceeded", "Không còn số lượng đã giao để trả.");
                    cost = decimal.Round(Math.Max(0, movements.Sum(x => -x.QuantityDelta * x.UnitCost)) / outstandingQuantity, 4, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                line.FulfilledQuantity += item.Quantity;
                if (order.Kind == OrderKind.Sales)
                {
                    (await BalanceAsync(order.WarehouseId, item.ProductId, ct)).Release(item.Quantity);
                    line.ReservedQuantity -= item.Quantity;
                }
            }
            doc.Lines.Add(new StockLine
            {
                ProductId = item.ProductId,
                Sku = line.Sku,
                ProductName = line.ProductName,
                Quantity = item.Quantity,
                UnitCost = incoming ? cost : null
            });
            amount += Money.Round((beforeQuantity + item.Quantity) * line.UnitPrice) - Money.Round(beforeQuantity * line.UnitPrice);
        }
        db.StockDocuments.Add(doc);
        order.RefreshStatus(); order.UpdatedAtUtc = Now;
        db.Entry(order).Property(x => x.UpdatedAtUtc).IsModified = true;
        await db.SaveChangesAsync(ct);
        await stock.PostAsync(doc.Id, actor, ct, linked: true);
        db.OrderFulfillments.Add(new OrderFulfillment
        {
            OrderId = id,
            RequestId = input.RequestId,
            StockDocumentId = doc.Id,
            IsReturn = input.IsReturn,
            CreatedAtUtc = Now,
            Amount = amount
        });
        await AddFinanceAsync(order, doc.Id, input.IsReturn, amount, ct);
        Audit(order, actor, input.IsReturn ? "ReturnOrder" : "FulfillOrder");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await DetailAsync(id, ct);
    }
    public async Task<OrderDetail> ReverseAsync(int id, int fulfillmentId, RequestActor actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockOrderAsync(id, ct);
        var order = await LoadAsync(id, ct);
        var original = await db.OrderFulfillments.SingleOrDefaultAsync(x => x.Id == fulfillmentId && x.OrderId == id, ct)
            ?? throw ErpException.Missing("fulfillment_missing", "Không tìm thấy phiếu thực hiện.");
        if (await db.OrderFulfillments.AnyAsync(x => x.ReversesId == fulfillmentId, ct)) return await DetailAsync(id, ct);
        if (original.ReversesId != null) throw ErpException.Conflict("invalid_reversal", "Không thể đảo phiếu đảo.");
        var lines = await db.StockDocuments.Where(x => x.Id == original.StockDocumentId).SelectMany(x => x.Lines).ToArrayAsync(ct);
        foreach (var item in lines)
        {
            var line = order.Lines.Single(x => x.ProductId == item.ProductId);
            if (original.IsReturn) line.ReturnedQuantity -= item.Quantity;
            else
            {
                if (line.FulfilledQuantity - item.Quantity < line.ReturnedQuantity)
                    throw ErpException.Conflict("return_exists", "Hãy đảo các phiếu trả trước.");
                line.FulfilledQuantity -= item.Quantity;
            }
        }
        var reversal = await stock.ReverseAsync(original.StockDocumentId, actor, ct, linked: true);
        if (!original.IsReturn && order.Kind == OrderKind.Sales && order.Status != OrderStatus.Cancelled)
            foreach (var item in lines)
            {
                (await BalanceAsync(order.WarehouseId, item.ProductId, ct)).Reserve(item.Quantity);
                order.Lines.Single(x => x.ProductId == item.ProductId).ReservedQuantity += item.Quantity;
            }
        order.RefreshStatus(); order.UpdatedAtUtc = Now;
        db.Entry(order).Property(x => x.UpdatedAtUtc).IsModified = true;
        db.OrderFulfillments.Add(new OrderFulfillment
        {
            OrderId = id,
            RequestId = Guid.NewGuid(),
            StockDocumentId = reversal.Document.Id,
            IsReturn = !original.IsReturn,
            ReversesId = original.Id,
            CreatedAtUtc = Now,
            Amount = original.Amount
        });
        await AddFinanceAsync(order, reversal.Document.Id, !original.IsReturn, original.Amount, ct);
        Audit(order, actor, "ReverseFulfillment");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await DetailAsync(id, ct);
    }
    private async Task AddFinanceAsync(TradeOrder order, int stockId, bool credit, decimal amount, CancellationToken ct)
    {
        var movements = await db.StockMovements.Where(x => x.DocumentId == stockId).Select(x => new { x.ProductId, x.QuantityDelta, x.UnitCost }).ToArrayAsync(ct);
        var cost = movements.Sum(x => decimal.Round(Math.Abs(x.QuantityDelta) * x.UnitCost, 2, MidpointRounding.AwayFromZero));
        var reversesId = await db.StockDocuments.Where(x => x.Id == stockId).Select(x => x.ReversesId).SingleAsync(ct);
        var attributed = order.Kind == OrderKind.Sales && !await (
            from f in db.OrderFulfillments
            join l in db.Set<StockLine>() on f.StockDocumentId equals l.DocumentId
            where f.OrderId == order.Id && l.PostedSalesCost == null
            select l.ProductId).AnyAsync(ct);
        if (attributed)
        {
            var lines = await db.Set<StockLine>().Where(x => x.DocumentId == stockId).ToArrayAsync(ct);
            if (reversesId is { } sourceId)
            {
                var source = await db.Set<StockLine>().Where(x => x.DocumentId == sourceId)
                    .ToDictionaryAsync(x => x.ProductId, x => x.PostedSalesCost!.Value, ct);
                var sourceTotal = await db.Set<FinanceInvoice>().Where(x => x.StockDocumentId == sourceId)
                    .Select(x => x.CostAmount).SingleAsync(ct);
                if (source.Values.Sum() != sourceTotal)
                    throw ErpException.Conflict("inconsistent_sales_cost", "Giá vốn từng dòng không khớp hóa đơn nguồn; hãy đối soát trước khi đảo.");
                foreach (var line in lines) line.AttributeSalesCost(source[line.ProductId]);
            }
            else if (!credit)
            {
                foreach (var line in lines)
                {
                    var movement = movements.Single(x => x.ProductId == line.ProductId);
                    line.AttributeSalesCost(Money.Round(Math.Abs(movement.QuantityDelta) * movement.UnitCost));
                }
            }
            else
            {
                var products = lines.Select(l => l.ProductId).ToArray();
                var remaining = await (
                    from f in db.OrderFulfillments
                    join l in db.Set<StockLine>() on f.StockDocumentId equals l.DocumentId
                    join m in db.StockMovements on new { DocumentId = l.DocumentId, l.ProductId } equals new { m.DocumentId, m.ProductId }
                    where f.OrderId == order.Id && m.WarehouseId == order.WarehouseId && products.Contains(l.ProductId)
                    group new { m.QuantityDelta, f.IsReturn, l.PostedSalesCost } by l.ProductId into sku
                    select new { ProductId = sku.Key, Quantity = sku.Sum(x => -x.QuantityDelta),
                        Cost = sku.Sum(x => x.IsReturn ? -x.PostedSalesCost!.Value : x.PostedSalesCost!.Value) }
                ).ToDictionaryAsync(x => x.ProductId, ct);
                foreach (var line in lines)
                {
                    if (!remaining.TryGetValue(line.ProductId, out var sku) || sku.Quantity < line.Quantity || sku.Cost < 0)
                        throw ErpException.Conflict("inconsistent_sales_cost", "Dữ liệu giá vốn nguồn không khớp; hãy đối soát đơn hàng.");
                    // Partial returns share the recorded remaining SKU cost; its final return receives the exact residual cents.
                    line.AttributeSalesCost(line.Quantity == sku.Quantity ? sku.Cost
                        : Math.Min(sku.Cost, Money.Round(sku.Cost * line.Quantity / sku.Quantity)));
                }
            }
            cost = lines.Sum(x => x.PostedSalesCost!.Value);
        }
        else if (reversesId is { } originalId)
            cost = await db.Set<FinanceInvoice>().Where(x => x.StockDocumentId == originalId).Select(x => x.CostAmount).SingleAsync(ct);
        else if (credit && order.Kind == OrderKind.Sales)
        {
            var outstandingCost = await db.Set<FinanceInvoice>().Where(x => x.OrderId == order.Id)
                .SumAsync(x => x.IsCredit ? -x.CostAmount : x.CostAmount, ct);
            // ponytail: invoices carry document totals; reconcile the last return across all SKUs here.
            cost = order.Lines.All(x => x.FulfilledQuantity == x.ReturnedQuantity)
                ? Math.Max(0, outstandingCost) : Math.Min(cost, Math.Max(0, outstandingCost));
        }
        await finance.AddInvoiceAsync(new FinanceSource(stockId, order.Id, Number(order.Kind, order.Id), order.PartnerId,
            order.Kind == OrderKind.Purchase ? FinanceDirection.Payable : FinanceDirection.Receivable, credit, amount, cost, Now), ct);
    }
    private Task LockOrderAsync(int id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM orders.Orders WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}", ct);

    private async Task<TradeOrder> LoadAsync(int id, CancellationToken ct) => await db.Orders.Include(x => x.Lines).Include(x => x.Partner).Include(x => x.Warehouse)
        .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ErpException.Missing("order_missing", "Không tìm thấy đơn.");
    private async Task<StockBalance> BalanceAsync(Guid warehouse, Guid product, CancellationToken ct) =>
        await db.StockBalances.SingleOrDefaultAsync(x => x.WarehouseId == warehouse && x.ProductId == product, ct)
        ?? throw ErpException.Conflict("insufficient_stock", "Sản phẩm chưa có tồn tại kho.");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private void Audit(TradeOrder order, RequestActor actor, string action) => db.AuditEntries.Add(AuditEntry.Create(actor, Now, action, "Order", order.Id.ToString(), after: order.Status.ToString()));
    private static OrderRow Row(TradeOrder x) => new(x.Id, Number(x.Kind, x.Id), x.Kind, x.Status, x.PartnerId, x.Partner.Name, x.WarehouseId, x.Warehouse.Name,
        x.Lines.Sum(l => decimal.Round(l.Quantity * l.UnitPrice, 2, MidpointRounding.AwayFromZero)), x.CreatedAtUtc, Convert.ToBase64String(x.Version));
}
