using MiniErp.Domain.Finance;

namespace MiniErp.Application.Finance;

public sealed record PartnerStatementRow(string Kind, long SourceId, int OrderId, string OrderNumber, long InvoiceId,
    long? PaymentId, DateTime AtUtc, DateTime? DueAtUtc, decimal Amount, decimal? Outstanding, bool IsOverdue);

public sealed record PartnerStatementPage(IReadOnlyList<PartnerStatementRow> Items, int PageNumber, int PageSize,
    int Total, FinanceCutoff Cutoff, string PartnerName, FinanceDirection Direction, decimal ClosingBalance, decimal Overdue);
