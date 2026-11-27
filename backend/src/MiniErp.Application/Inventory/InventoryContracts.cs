using System.ComponentModel.DataAnnotations;
using MiniErp.Domain.Inventory;

namespace MiniErp.Application.Inventory;

public sealed record StockLineInput(Guid ProductId, decimal Quantity, decimal? UnitCost);
public sealed record StockDocumentEditInput(byte[] Version, Guid WarehouseId, Guid? DestinationId,
    [property: StringLength(300)] string? Note, StockLineInput[] Lines, bool Recount = false);
public sealed record StockPostInput(byte[] Version);
public sealed record StockCancelInput(byte[] Version, [property: Required, StringLength(300)] string Reason);

/// <summary>New stock document with a client-generated request identifier for safe retry.</summary>
public sealed record StockDocumentInput(Guid RequestId, StockKind Kind, Guid WarehouseId, Guid? DestinationId,
    [property: StringLength(300)] string? Note, [property: Required, Length(1, 50)] StockLineInput[] Lines);

public sealed record StockLineResponse(Guid ProductId, string Sku, string ProductName, decimal Quantity, decimal? UnitCost);

public sealed record StockDocumentRow(int Id, string Number, StockKind Kind, StockStatus Status, Guid WarehouseId,
    string WarehouseName, string? DestinationName, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, int? ReversesId,
    byte[] Version);

public sealed record StockDocumentResponse(StockDocumentRow Document, string? Note, Guid? DestinationId,
    IReadOnlyList<StockLineResponse> Lines, IReadOnlyList<DocumentEvent> History);

public sealed record StockBalanceRow(Guid WarehouseId, string WarehouseName, Guid ProductId, string Sku,
    string ProductName, decimal OnHand, decimal Reserved, decimal Available, decimal? AverageCost,
    decimal MinimumStock, bool IsLowStock);

public sealed record StockMovementRow(long Id, int DocumentId, string DocumentNumber, Guid WarehouseId,
    string WarehouseName, Guid ProductId, string Sku, decimal QuantityDelta, decimal? UnitCost,
    decimal BalanceAfter, DateTimeOffset PostedAt);

public sealed record StockProductOption(Guid Id, string Sku, string Name, decimal? PurchasePrice, decimal SalePrice);

public sealed record StockReconciliationRow(Guid WarehouseId, string Warehouse, Guid ProductId, string Sku,
    decimal OnHand, decimal LedgerQuantity, decimal Reserved, decimal OrderReservations,
    decimal OpenCommitment, int InconsistentOrderLines, bool HasBalance);
