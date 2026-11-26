using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Finance;
using MiniErp.Domain;
using MiniErp.Domain.Finance;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Finance;

public sealed class FinanceReport(ErpDbContext db, TimeProvider clock)
{
    public async Task<AgingRow[]> AgingAsync(DateTime? asOf, FinanceDirection? direction, CancellationToken ct)
    {
        FinanceService.ValidateDirection(direction);
        var at = AsOf(asOf);
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        var command = new CommandDefinition("""
              SELECT i.Id,i.OrderId,i.PartnerId,p.Name PartnerName,i.Direction,i.IsCredit,i.Amount,COALESCE(a.Paid,0) Paid,i.DueAtUtc
              FROM finance.Invoices i JOIN catalog.BusinessPartners p ON p.Id=i.PartnerId
              OUTER APPLY (SELECT SUM(a.Amount) Paid FROM finance.Allocations a
                JOIN finance.Payments pay ON pay.Id=a.PaymentId WHERE a.InvoiceId=i.Id AND pay.PostedAtUtc<=@At) a
              WHERE i.IssuedAtUtc<=@At AND (@Direction IS NULL OR i.Direction=@Direction)
            """, new { At = at, Direction = (byte?)direction }, cancellationToken: ct);
        var sources=(await connection.QueryAsync<HistoricalInvoice>(command)).ToArray();
        var settlements=sources.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Paid))))
            .ToDictionary(x=>x.Key,x=>x.Value);
        return sources.GroupBy(x=>new{x.PartnerId,x.PartnerName,x.Direction}).Select(g=>
        {
            decimal Bucket(Func<int,bool> include)=>g.Where(x=>include((at.Date-x.DueAtUtc.Date).Days)).Sum(x=>settlements[x.Id].Outstanding*(x.IsCredit?-1:1));
            return new AgingRow(g.Key.PartnerId,g.Key.PartnerName,g.Key.Direction,Bucket(age=>age<=0),Bucket(age=>age is >=1 and <=30),
                Bucket(age=>age is >=31 and <=60),Bucket(age=>age is >=61 and <=90),Bucket(age=>age>90),Bucket(_=>true));
        }).OrderBy(x=>x.PartnerName).ThenBy(x=>x.PartnerId).ThenBy(x=>x.Direction).ToArray();
    }
    private sealed record HistoricalInvoice(long Id,int OrderId,Guid PartnerId,string PartnerName,FinanceDirection Direction,bool IsCredit,decimal Amount,decimal Paid,DateTime DueAtUtc);

    public async Task<ReconciliationRow[]> ReconciliationAsync(DateTime? asOf, CancellationToken ct)
    {
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        var command = new CommandDefinition("""
            WITH D AS (SELECT CAST(1 AS tinyint) Direction UNION ALL SELECT CAST(2 AS tinyint)),
            I AS (SELECT Direction,SUM(CASE WHEN IsCredit=0 THEN Amount ELSE 0 END) Invoiced,
              SUM(CASE WHEN IsCredit=1 THEN Amount ELSE 0 END) Credits
              FROM finance.Invoices WHERE IssuedAtUtc<=@At GROUP BY Direction),
            P AS (SELECT Direction,SUM(CASE WHEN IsRefund=0 THEN Amount ELSE 0 END) Paid,
              SUM(CASE WHEN IsRefund=1 THEN Amount ELSE 0 END) Refunded
              FROM finance.Payments WHERE PostedAtUtc<=@At GROUP BY Direction),
            A AS (SELECT i.Direction,SUM((i.Amount-COALESCE(a.Paid,0))*CASE WHEN i.IsCredit=1 THEN -1 ELSE 1 END) Outstanding
              FROM finance.Invoices i OUTER APPLY (SELECT SUM(a.Amount) Paid FROM finance.Allocations a
                JOIN finance.Payments p ON p.Id=a.PaymentId WHERE a.InvoiceId=i.Id AND p.PostedAtUtc<=@At) a
              WHERE i.IssuedAtUtc<=@At GROUP BY i.Direction)
            SELECT d.Direction,COALESCE(i.Invoiced,0) Invoiced,COALESCE(i.Credits,0) Credits,
              COALESCE(p.Paid,0) Paid,COALESCE(p.Refunded,0) Refunded,
              COALESCE(i.Invoiced,0)-COALESCE(i.Credits,0)-COALESCE(p.Paid,0)+COALESCE(p.Refunded,0) Balance,
              COALESCE(a.Outstanding,0) Outstanding,
              COALESCE(i.Invoiced,0)-COALESCE(i.Credits,0)-COALESCE(p.Paid,0)+COALESCE(p.Refunded,0)-COALESCE(a.Outstanding,0) Difference
            FROM D d LEFT JOIN I i ON i.Direction=d.Direction LEFT JOIN P p ON p.Direction=d.Direction LEFT JOIN A a ON a.Direction=d.Direction
            """, new { At = AsOf(asOf) }, cancellationToken: ct);
        return (await connection.QueryAsync<ReconciliationRow>(command)).ToArray();
    }

    private DateTime AsOf(DateTime? at)
    {
        if (at.HasValue && (at.Value.Kind != DateTimeKind.Utc || at.Value.Year<1753))
            throw ErpException.Invalid("utc_required", "Ngày báo cáo cần có múi giờ UTC (Z).");
        return at ?? clock.GetUtcNow().UtcDateTime;
    }
}
