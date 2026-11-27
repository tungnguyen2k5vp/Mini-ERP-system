namespace MiniErp.Domain.Finance;

public enum FinanceDirection : byte { Receivable = 1, Payable = 2 }

public static class Money
{
    public static decimal Round(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    public static decimal Validate(decimal amount, bool allowZero = false)
    {
        if (amount < 0 || (!allowZero && amount == 0) || amount > 999_999_999_999m || Round(amount) != amount)
            throw ErpException.Invalid("invalid_money", "Số tiền phải dương, tối đa 12 chữ số và 2 chữ số thập phân.");
        return amount;
    }
}

public sealed class FinanceInvoice
{
    public long Id { get; private set; }
    public int StockDocumentId { get; init; }
    public int OrderId { get; init; }
    public string OrderNumber { get; init; } = "";
    public Guid PartnerId { get; init; }
    public FinanceDirection Direction { get; init; }
    public bool IsCredit { get; init; }
    public decimal Amount { get; init; }
    public decimal CostAmount { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public DateTime DueAtUtc { get; init; }
    public List<PaymentAllocation> Allocations { get; init; } = [];
}

public sealed class FinancePayment
{
    public long Id { get; private set; }
    public Guid RequestId { get; init; }
    public Guid PartnerId { get; init; }
    public FinanceDirection Direction { get; init; }
    public bool IsRefund { get; init; }
    public Guid PaymentMethodId { get; init; }
    public MiniErp.Domain.Catalog.LookupKind PaymentMethodKind { get; private set; } = MiniErp.Domain.Catalog.LookupKind.PaymentMethod;
    public decimal Amount { get; init; }
    public string? Reference { get; init; }
    public DateTime PostedAtUtc { get; init; }
    public List<PaymentAllocation> Allocations { get; init; } = [];
}

public sealed class PaymentReversal
{
    public long Id { get; private set; }
    public long PaymentId { get; init; }
    public Guid RequestId { get; init; }
    public DateTime PostedAtUtc { get; init; }
    public string Reason { get; init; } = "";
    public Guid? UserId { get; init; }
}

public sealed class PaymentAllocation
{
    public long PaymentId { get; private set; }
    public long InvoiceId { get; init; }
    public decimal Amount { get; init; }
}
