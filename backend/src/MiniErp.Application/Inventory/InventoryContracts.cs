using System.ComponentModel.DataAnnotations;
using MiniErp.Domain.Inventory;

namespace MiniErp.Application.Inventory;

public sealed record StockLineInput(Guid ProductId, decimal Quantity, decimal? UnitCost);

/// <summary>New stock document with a client-generated request identifier for safe retry.</summary>
public sealed record StockDocumentInput(Guid RequestId, StockKind Kind, Guid WarehouseId, Guid? DestinationId,
    [property: StringLength(300)] string? Note, [property: Required, Length(1, 50)] StockLineInput[] Lines);

public sealed record StockLineResponse(Guid ProductId, string Sku, string ProductName, decimal Quantity, decimal? UnitCost);

public sealed record StockDocumentRow(int Id, string Number, StockKind Kind, StockStatus Status, Guid WarehouseId,
    string WarehouseName, string? DestinationName, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, int? ReversesId);

public sealed record StockDocumentResponse(StockDocumentRow Document, string? Note, Guid? DestinationId,
    IReadOnlyList<StockLineResponse> Lines);

public sealed record StockBalanceRow(Guid WarehouseId, string WarehouseName, Guid ProductId, string Sku,
    string ProductName, decimal OnHand, decimal Reserved, decimal Available, decimal AverageCost,
    decimal MinimumStock, bool IsLowStock);

public sealed record StockMovementRow(long Id, int DocumentId, string DocumentNumber, Guid WarehouseId,
    string WarehouseName, Guid ProductId, string Sku, decimal QuantityDelta, decimal UnitCost,
    decimal BalanceAfter, DateTimeOffset PostedAt);

public sealed record StockProductOption(Guid Id, string Sku, string Name, decimal PurchasePrice);
