using MiniErp.Domain.Catalog;
namespace MiniErp.Domain.Orders;

public enum OrderKind : byte { Purchase = 1, Sales = 2 }
public enum OrderStatus : byte { Draft = 1, PendingApproval = 2, Approved = 3, Partial = 4, Fulfilled = 5, Cancelled = 6 }
public sealed class TradeOrder
{
    public int Id { get; set; }
    public Guid RequestId { get; init; }
    public OrderKind Kind { get; init; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public Guid PartnerId { get; init; }
    public BusinessPartner Partner { get; set; } = null!;
    public Guid WarehouseId { get; init; }
    public LookupKind WarehouseKind { get; private set; } = LookupKind.Warehouse;
    public CatalogLookup Warehouse { get; set; } = null!;
    public string? Note { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
    public List<OrderLine> Lines { get; init; } = [];
    public void Submit() { Require(OrderStatus.Draft); Status = OrderStatus.PendingApproval; }
    public void Approve() { Require(OrderStatus.PendingApproval); Status = OrderStatus.Approved; }
    public void Cancel()
    {
        if (Status == OrderStatus.Fulfilled) throw ErpException.Conflict("invalid_status", "Đơn đã hoàn tất; hãy lập phiếu trả hàng.");
        Status = OrderStatus.Cancelled;
    }
    public void RefreshStatus()
    {
        if (Status == OrderStatus.Cancelled) return;
        Status = Lines.All(x => x.FulfilledQuantity == x.Quantity) ? OrderStatus.Fulfilled
            : Lines.Any(x => x.FulfilledQuantity > 0) ? OrderStatus.Partial : OrderStatus.Approved;
    }
    public void RequireFulfillable()
    {
        if (Status is not (OrderStatus.Approved or OrderStatus.Partial or OrderStatus.Fulfilled or OrderStatus.Cancelled))
            throw ErpException.Conflict("invalid_status", "Đơn chưa được duyệt.");
    }
    private void Require(OrderStatus status)
    {
        if (Status != status) throw ErpException.Conflict("invalid_status", "Trạng thái đơn không phù hợp.");
    }
}
public sealed class OrderLine
{
    public int OrderId { get; set; }
    public Guid ProductId { get; init; }
    public string Sku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal FulfilledQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
}
public sealed class OrderFulfillment
{
    public int Id { get; set; }
    public int OrderId { get; init; }
    public Guid RequestId { get; init; }
    public int StockDocumentId { get; set; }
    public bool IsReturn { get; init; }
    public int? ReversesId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public decimal Amount { get; init; }
}
