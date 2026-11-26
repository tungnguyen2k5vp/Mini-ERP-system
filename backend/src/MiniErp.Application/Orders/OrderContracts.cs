using MiniErp.Domain.Orders;
namespace MiniErp.Application.Orders;
public sealed record OrderLineInput(Guid ProductId, decimal Quantity, decimal UnitPrice);
public sealed record OrderInput(Guid RequestId, OrderKind Kind, Guid PartnerId, Guid WarehouseId, string? Note, OrderLineInput[] Lines);
public sealed record OrderVersion(string Version);
public sealed record FulfillmentLineInput(Guid ProductId, decimal Quantity);
public sealed record FulfillmentInput(Guid RequestId, bool IsReturn, FulfillmentLineInput[] Lines);
public sealed record OrderRow(int Id, string Number, OrderKind Kind, OrderStatus Status, Guid PartnerId, string PartnerName,
    Guid WarehouseId, string WarehouseName, decimal? Total, DateTime CreatedAtUtc, string Version);
public sealed record OrderLineResponse(Guid ProductId, string Sku, string ProductName, decimal Quantity, decimal? UnitPrice,
    decimal FulfilledQuantity, decimal ReturnedQuantity, decimal ReservedQuantity);
public sealed record FulfillmentResponse(int Id, int StockDocumentId, bool IsReturn, int? ReversesId, DateTime CreatedAtUtc, decimal? Amount);
public sealed record OrderDetail(OrderRow Order, string? Note, OrderLineResponse[] Lines, FulfillmentResponse[] Fulfillments);
public sealed record OrderOption(Guid Id,string Name,bool IsActive);
public sealed record OrderProductOption(Guid Id,string Sku,string Name,decimal? PurchasePrice,decimal SalePrice);
public sealed record OrderOptions(OrderOption[] Partners,OrderOption[] Warehouses,OrderProductOption[] Products);
