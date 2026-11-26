using MiniErp.Domain.Finance;

namespace MiniErp.Application.Finance;

public sealed record FinanceSource(int StockDocumentId, int OrderId, string OrderNumber, Guid PartnerId,
    FinanceDirection Direction, bool IsCredit, decimal Amount, decimal CostAmount, DateTime OccurredAtUtc);
public interface IFinancePosting
{
    Task AddInvoiceAsync(FinanceSource source, CancellationToken ct);
}
public sealed record AllocationInput(long InvoiceId, decimal Amount);
public sealed record PaymentInput(Guid RequestId, Guid PartnerId, FinanceDirection Direction, bool IsRefund,
    Guid PaymentMethodId, decimal Amount, string? Reference, AllocationInput[] Allocations);
public sealed record InvoiceRow(long Id, int StockDocumentId, int OrderId, string OrderNumber, Guid PartnerId,
    string PartnerName, FinanceDirection Direction, bool IsCredit, decimal Amount, decimal CostAmount,
    decimal Settled, decimal Outstanding, DateTime IssuedAtUtc, DateTime DueAtUtc,decimal OffsetAmount=0,decimal ActionableAmount=0);
public sealed record PaymentRow(long Id, Guid RequestId, Guid PartnerId, FinanceDirection Direction, bool IsRefund,
    Guid PaymentMethodId, decimal Amount, string? Reference, DateTime PostedAtUtc, AllocationInput[] Allocations);
public sealed record AgingRow(Guid PartnerId, string PartnerName, FinanceDirection Direction, decimal Current,
    decimal Days1To30, decimal Days31To60, decimal Days61To90, decimal DaysOver90, decimal Balance);
public sealed record ReconciliationRow(FinanceDirection Direction, decimal Invoiced, decimal Credits,
    decimal Paid, decimal Refunded, decimal Balance, decimal Outstanding, decimal Difference);
public sealed record FinanceOption(Guid Id,string Name,bool IsActive);
public sealed record FinanceOptions(FinanceOption[] Partners,FinanceOption[] Methods);
