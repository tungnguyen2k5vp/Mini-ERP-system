using MiniErp.Domain.Catalog;

namespace MiniErp.Domain.Inventory;

public enum StockKind : byte { Receipt = 1, Issue = 2, Transfer = 3, Adjustment = 4, Count = 5, Reversal = 6 }
public enum StockStatus : byte { Draft = 1, Posted = 2, Cancelled = 3, Reversed = 4 }

public sealed class StockBalance
{
    public Guid WarehouseId { get; init; }
    public LookupKind WarehouseKind { get; private set; } = LookupKind.Warehouse;
    public Guid ProductId { get; init; }
    public decimal OnHand { get; private set; }
    public decimal Reserved { get; private set; }
    public decimal Available => OnHand - Reserved;
    public decimal AverageCost { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void Receive(decimal quantity, decimal unitCost)
    {
        RequirePositive(quantity);
        CatalogRules.NonNegative(unitCost);
        var after = CatalogRules.NonNegative(OnHand + quantity);
        AverageCost = decimal.Round((OnHand * AverageCost + quantity * unitCost) / after, 4, MidpointRounding.AwayFromZero);
        OnHand = after;
    }

    public decimal Issue(decimal quantity)
    {
        RequirePositive(quantity);
        if (quantity > Available)
        {
            throw ErpException.Conflict("insufficient_stock", "Số lượng xuất vượt tồn khả dụng.");
        }
        var cost = AverageCost;
        OnHand -= quantity;
        if (OnHand == 0) AverageCost = 0;
        return cost;
    }

    public void Reserve(decimal quantity)
    {
        RequirePositive(quantity);
        if (quantity > Available) throw ErpException.Conflict("insufficient_stock", "Tồn khả dụng không đủ để giữ hàng.");
        Reserved += quantity;
    }

    public void Release(decimal quantity)
    {
        RequirePositive(quantity);
        if (quantity > Reserved) throw ErpException.Conflict("invalid_reservation", "Số lượng giữ hàng không hợp lệ.");
        Reserved -= quantity;
    }

    public void Reverse(decimal originalDelta, decimal previousAverageCost)
    {
        var restored = OnHand - originalDelta;
        if (restored < Reserved)
        {
            throw ErpException.Conflict("insufficient_stock", "Tồn khả dụng không đủ để đảo giao dịch.");
        }
        OnHand = CatalogRules.NonNegative(restored);
        AverageCost = previousAverageCost;
    }

    public static void RequirePositive(decimal amount)
    {
        if (CatalogRules.NonNegative(amount) == 0)
        {
            throw ErpException.Invalid("invalid_quantity", "Số lượng cần lớn hơn 0.");
        }
    }
}

public sealed class StockDocument
{
    public int Id { get; private set; }
    public Guid RequestId { get; init; }
    public StockKind Kind { get; init; }
    public StockStatus Status { get; private set; } = StockStatus.Draft;
    public LookupKind WarehouseKind { get; private set; } = LookupKind.Warehouse;
    public Guid WarehouseId { get; init; }
    public CatalogLookup Warehouse { get; private set; } = null!;
    public Guid? DestinationId { get; init; }
    public CatalogLookup? Destination { get; private set; }
    public string? Note { get; init; }
    public int? ReversesId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? PostedAtUtc { get; private set; }
    public byte[] Version { get; private set; } = [];
    public List<StockLine> Lines { get; init; } = [];

    public void Post(DateTime utc)
    {
        RequireDraft();
        PostedAtUtc = utc;
        Status = StockStatus.Posted;
    }

    public void Cancel()
    {
        RequireDraft();
        Status = StockStatus.Cancelled;
    }

    public void MarkReversed()
    {
        if (Status != StockStatus.Posted || Kind == StockKind.Reversal)
        {
            throw ErpException.Conflict("invalid_status", "Chỉ có thể đảo chứng từ gốc đã ghi sổ.");
        }
        Status = StockStatus.Reversed;
    }

    private void RequireDraft()
    {
        if (Status != StockStatus.Draft)
        {
            throw ErpException.Conflict("invalid_status", "Thao tác này chỉ áp dụng cho chứng từ nháp.");
        }
    }
}

public sealed class StockLine
{
    public int DocumentId { get; private set; }
    public Guid ProductId { get; init; }
    public string Sku { get; init; } = "";
    public string ProductName { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public decimal? PostedSalesCost { get; private set; }
    public byte[]? CountVersion { get; init; }

    public void AttributeSalesCost(decimal amount)
    {
        CatalogRules.NonNegative(amount);
        if (decimal.Round(amount, 2) != amount)
            throw ErpException.Invalid("invalid_sales_cost", "Giá vốn ghi nhận cần tối đa 2 chữ số thập phân.");
        if (PostedSalesCost is { } existing && existing != amount)
            throw ErpException.Conflict("sales_cost_already_recorded", "Không thể sửa giá vốn đã ghi nhận.");
        PostedSalesCost = amount;
    }
}

public sealed class StockMovement
{
    public long Id { get; private set; }
    public int DocumentId { get; init; }
    public StockDocument Document { get; init; } = null!;
    public LookupKind WarehouseKind { get; private set; } = LookupKind.Warehouse;
    public Guid WarehouseId { get; init; }
    public Guid ProductId { get; init; }
    public decimal QuantityDelta { get; init; }
    public decimal UnitCost { get; init; }
    public decimal PreviousAverageCost { get; init; }
    public decimal BalanceAfter { get; init; }
    public DateTime PostedAtUtc { get; init; }
}
