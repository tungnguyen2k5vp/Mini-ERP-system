using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Application.Finance;
using MiniErp.Application.Reporting;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;

namespace MiniErp.Infrastructure.Finance;

public sealed partial class FinanceReport
{
    public async Task<PartnerStatementPage> StatementAsync(Guid partnerId, DateTime? asOf, DateOnly? asOfDate,
        int page, int size, CancellationToken ct)
    {
        if (partnerId == Guid.Empty || page is < 1 or > 100_000 || size is < 1 or > 100)
            throw ErpException.Invalid("invalid_statement", "Chọn đối tác và trang hợp lệ.");
        var cutoff = Cutoff(asOf, asOfDate);
        await using var owned = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        RequireConsistentTransaction(transaction.IsolationLevel);
        var partner = await db.Partners.Where(x => x.Id == partnerId).Select(x => new { x.Name, x.Kind })
            .SingleOrDefaultAsync(ct) ?? throw ErpException.Missing("partner_missing", "Không tìm thấy đối tác.");
        var direction = partner.Kind == PartnerKind.Customer ? FinanceDirection.Receivable : FinanceDirection.Payable;
        var parameters = Parameters(cutoff);
        parameters.Add("Partner", partnerId);
        parameters.Add("Direction", (byte)direction, DbType.Byte);
        parameters.Add("DueBefore", BusinessCalendar.StartUtc(cutoff.BusinessDate), DbType.DateTime2);
        parameters.Add("Skip", (page - 1) * size);
        parameters.Add("Take", size);
        using var results = await db.Database.GetDbConnection().QueryMultipleAsync(
            new CommandDefinition(StatementSql, parameters, transaction, cancellationToken: ct));
        var totals = await results.ReadSingleAsync<StatementTotals>();
        var count = await results.ReadSingleAsync<int>();
        var rows = (await results.ReadAsync<PartnerStatementRow>()).AsList();
        if (owned is not null) await owned.CommitAsync(ct);
        return new(rows, page, size, count, cutoff, partner.Name, direction, totals.ClosingBalance, totals.Overdue);
    }

    private sealed record StatementTotals(decimal ClosingBalance, decimal Overdue);

    // The running debit total applies the order's available credit to the oldest invoices, as in Settlement.Calculate.
    private const string StatementSql = """
        WITH InvoiceBalances AS (
            SELECT i.Id,i.OrderId,i.OrderNumber,i.IsCredit,i.IssuedAtUtc,i.DueAtUtc,i.Amount,
                   i.Amount-COALESCE(a.Paid,0) Residual
            FROM finance.Invoices i
            OUTER APPLY (
                SELECT CAST(SUM(CASE WHEN e.IsReversal=1 THEN -a.Amount ELSE a.Amount END) AS decimal(28,2)) Paid
                FROM finance.Allocations a JOIN finance.PaymentEvents e ON e.PaymentId=a.PaymentId
                WHERE a.InvoiceId=i.Id AND (e.PostedAtUtc<@At OR (@Inclusive=1 AND e.PostedAtUtc=@At))
            ) a
            WHERE i.PartnerId=@Partner AND i.Direction=@Direction
              AND (i.IssuedAtUtc<@At OR (@Inclusive=1 AND i.IssuedAtUtc=@At))
        ), OrderTotals AS (
            SELECT b.*,
                CAST(SUM(CASE WHEN IsCredit=1 THEN Residual ELSE 0 END) OVER(PARTITION BY OrderId) AS decimal(28,2)) Credits,
                CAST(SUM(CASE WHEN IsCredit=0 THEN Residual ELSE 0 END)
                    OVER(PARTITION BY OrderId ORDER BY Id ROWS UNBOUNDED PRECEDING) AS decimal(28,2)) DebitThrough
            FROM InvoiceBalances b
        )
        SELECT *,CASE WHEN IsCredit=1 THEN NULL WHEN DebitThrough<=Credits THEN 0
                      WHEN DebitThrough-Credits<Residual THEN DebitThrough-Credits ELSE Residual END Outstanding
        INTO #StatementInvoices FROM OrderTotals;

        SELECT COALESCE(SUM(CASE WHEN IsCredit=1 THEN -Residual ELSE Residual END),0) ClosingBalance,
               COALESCE(SUM(CASE WHEN DueAtUtc<@DueBefore THEN Outstanding ELSE 0 END),0) Overdue
        FROM #StatementInvoices;
        WITH Events AS (
            SELECT CASE WHEN i.IsCredit=1 THEN 'Credit' ELSE 'Invoice' END Kind,i.Id SourceId,i.OrderId,i.OrderNumber,
                   i.Id InvoiceId,CAST(NULL AS bigint) PaymentId,i.IssuedAtUtc AtUtc,i.DueAtUtc,
                   CASE WHEN i.IsCredit=1 THEN -i.Amount ELSE i.Amount END Amount,i.Outstanding,
                   CAST(CASE WHEN i.Outstanding>0 AND i.DueAtUtc<@DueBefore THEN 1 ELSE 0 END AS bit) IsOverdue,0 Position
            FROM #StatementInvoices i
            UNION ALL
            SELECT CASE WHEN e.IsReversal=1 THEN CASE WHEN e.IsRefund=1 THEN 'RefundReversal' ELSE 'PaymentReversal' END
                        WHEN e.IsRefund=1 THEN 'Refund' ELSE 'Payment' END,e.EventId,i.OrderId,i.OrderNumber,
                   i.Id,e.PaymentId,e.PostedAtUtc,NULL,
                   CASE WHEN e.IsRefund=e.IsReversal THEN -a.Amount ELSE a.Amount END,NULL,CAST(0 AS bit),
                   CASE WHEN e.IsReversal=1 THEN 2 ELSE 1 END
            FROM finance.PaymentEvents e JOIN finance.Allocations a ON a.PaymentId=e.PaymentId
            JOIN #StatementInvoices i ON i.Id=a.InvoiceId
            WHERE e.PartnerId=@Partner AND e.Direction=@Direction AND (e.PostedAtUtc<@At OR (@Inclusive=1 AND e.PostedAtUtc=@At))
        )
        SELECT * INTO #StatementEvents FROM Events;
        SELECT COUNT(*) FROM #StatementEvents;
        SELECT Kind,SourceId,OrderId,OrderNumber,InvoiceId,PaymentId,AtUtc,DueAtUtc,Amount,Outstanding,IsOverdue
        FROM #StatementEvents ORDER BY AtUtc DESC,Position DESC,SourceId DESC,InvoiceId DESC
        OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
        DROP TABLE #StatementEvents;
        DROP TABLE #StatementInvoices;
        """;
}
