using MiniErp.Domain.Catalog;
namespace MiniErp.Domain.Orders;

public enum OrderKind : byte { Purchase = 1, Sales = 2 }
public enum OrderStatus : byte { Draft = 1, PendingApproval = 2, Approved = 3, Partial = 4, Fulfilled = 5, Cancelled = 6, Rejected = 7, ReturnedForRevision = 8 }
public sealed class TradeOrder
{
    private Guid _partnerId;
    private Guid _warehouseId;
    private string? _note;
    private DateOnly? _expectedDate;
    public int Id { get; set; }
    public Guid RequestId { get; init; }
    public OrderKind Kind { get; init; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public Guid PartnerId { get => _partnerId; init => _partnerId = value; }
    public BusinessPartner Partner { get; set; } = null!;
    public Guid WarehouseId { get => _warehouseId; init => _warehouseId = value; }
    public LookupKind WarehouseKind { get; private set; } = LookupKind.Warehouse;
    public CatalogLookup Warehouse { get; set; } = null!;
    public string? Note { get => _note; init => _note = value; }
    public DateOnly? ExpectedDate { get => _expectedDate; init => _expectedDate = value; }
    public string? CreatePayloadHash { get; private set; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] Version { get; set; } = [];
    public List<OrderLine> Lines { get; init; } = [];
    public void Submit() { Require(OrderStatus.Draft); Status = OrderStatus.PendingApproval; }
    public void Approve() { Require(OrderStatus.PendingApproval); Status = OrderStatus.Approved; }
    public void Reject() { Require(OrderStatus.PendingApproval); Status = OrderStatus.Rejected; }
    public void ReturnForRevision() { Require(OrderStatus.PendingApproval); Status = OrderStatus.ReturnedForRevision; }
    public void FreezeCreatePayload(string hash) => CreatePayloadHash ??= hash;
    public void Edit(Guid partnerId, Guid warehouseId, string? note, DateOnly? expectedDate)
    {
        Edit();
        _partnerId = partnerId;
        _warehouseId = warehouseId;
        _note = note;
        _expectedDate = expectedDate;
    }
    public void Edit()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.ReturnedForRevision))
            throw ErpException.Conflict("invalid_status", "Chỉ sửa đơn nháp hoặc đơn được trả lại.");
        Status = OrderStatus.Draft;
    }
    public void Cancel()
    {
        if (Status == OrderStatus.Cancelled) return;
        if (Status is not (OrderStatus.Draft or OrderStatus.ReturnedForRevision or OrderStatus.PendingApproval or OrderStatus.Approved or OrderStatus.Partial))
            throw ErpException.Conflict("invalid_status", Status == OrderStatus.Rejected ? "Đơn đã bị từ chối." : "Đơn đã hoàn tất; hãy lập phiếu trả hàng.");
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
