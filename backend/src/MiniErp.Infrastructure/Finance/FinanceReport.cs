using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Application.Finance;
using MiniErp.Application.Reporting;
using MiniErp.Domain;
using MiniErp.Domain.Finance;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Finance;

public sealed partial class FinanceReport(ErpDbContext db, TimeProvider clock)
{
    public async Task<AgingRow[]> AgingAsync(DateTime? asOf, FinanceDirection? direction, CancellationToken ct,DateOnly? asOfDate=null)
    {
        FinanceService.ValidateDirection(direction);
        var cutoff = Cutoff(asOf,asOfDate);
        await using var ownedTransaction=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var connection=db.Database.GetDbConnection();
        var transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        RequireConsistentTransaction(transaction.IsolationLevel);
        var parameters=Parameters(cutoff);
        parameters.Add("Direction",(byte?)direction,DbType.Byte);
        var command = new CommandDefinition("""
              SELECT i.Id,i.OrderId,i.PartnerId,p.Name PartnerName,i.Direction,i.IsCredit,i.Amount,COALESCE(a.Paid,0) Paid,i.DueAtUtc
              FROM finance.Invoices i JOIN catalog.BusinessPartners p ON p.Id=i.PartnerId
              OUTER APPLY (SELECT SUM(CASE WHEN pay.IsReversal=1 THEN -a.Amount ELSE a.Amount END) Paid FROM finance.Allocations a
                JOIN finance.PaymentEvents pay ON pay.PaymentId=a.PaymentId WHERE a.InvoiceId=i.Id AND (pay.PostedAtUtc<@At OR (@Inclusive=1 AND pay.PostedAtUtc=@At))) a
              WHERE (i.IssuedAtUtc<@At OR (@Inclusive=1 AND i.IssuedAtUtc=@At)) AND (@Direction IS NULL OR i.Direction=@Direction)
            """, parameters,transaction:transaction,cancellationToken: ct);
        var sources=(await connection.QueryAsync<HistoricalInvoice>(command)).ToArray();
        var settlements=sources.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Paid))))
            .ToDictionary(x=>x.Key,x=>x.Value);
        var result=sources.GroupBy(x=>new{x.PartnerId,x.PartnerName,x.Direction}).Select(g=>
        {
            decimal Bucket(Func<int,bool> include)=>g.Where(x=>include(cutoff.BusinessDate.DayNumber-DueDate(x.DueAtUtc).DayNumber)).Sum(x=>settlements[x.Id].Outstanding*(x.IsCredit?-1:1));
            return new AgingRow(g.Key.PartnerId,g.Key.PartnerName,g.Key.Direction,Bucket(age=>age<=0),Bucket(age=>age is >=1 and <=30),
                Bucket(age=>age is >=31 and <=60),Bucket(age=>age is >=61 and <=90),Bucket(age=>age>90),Bucket(_=>true));
        }).OrderBy(x=>x.PartnerName).ThenBy(x=>x.PartnerId).ThenBy(x=>x.Direction).ToArray();
        if(ownedTransaction is not null)await ownedTransaction.CommitAsync(ct);
        return result;
    }
    private sealed record HistoricalInvoice(long Id,int OrderId,Guid PartnerId,string PartnerName,FinanceDirection Direction,bool IsCredit,decimal Amount,decimal Paid,DateTime DueAtUtc);

    public async Task<ReconciliationRow[]> ReconciliationAsync(DateTime? asOf, CancellationToken ct,DateOnly? asOfDate=null)
    {
        var cutoff=Cutoff(asOf,asOfDate);
        await using var ownedTransaction=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var connection=db.Database.GetDbConnection();
        var transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        RequireConsistentTransaction(transaction.IsolationLevel);
        var command = new CommandDefinition("""
            WITH D AS (SELECT CAST(1 AS tinyint) Direction UNION ALL SELECT CAST(2 AS tinyint)),
            I AS (SELECT Direction,CAST(SUM(CASE WHEN IsCredit=0 THEN Amount ELSE 0 END) AS decimal(28,2)) Invoiced,
              CAST(SUM(CASE WHEN IsCredit=1 THEN Amount ELSE 0 END) AS decimal(28,2)) Credits
              FROM finance.Invoices WHERE IssuedAtUtc<@At OR (@Inclusive=1 AND IssuedAtUtc=@At) GROUP BY Direction),
            P AS (SELECT Direction,CAST(SUM(CASE WHEN IsRefund=0 THEN Amount ELSE 0 END) AS decimal(28,2)) Paid,
              CAST(SUM(CASE WHEN IsRefund=1 THEN Amount ELSE 0 END) AS decimal(28,2)) Refunded
              FROM finance.PaymentEvents WHERE PostedAtUtc<@At OR (@Inclusive=1 AND PostedAtUtc=@At) GROUP BY Direction),
            A AS (SELECT i.Direction,CAST(SUM(CASE WHEN i.IsCredit=1 THEN COALESCE(a.Paid,0)-i.Amount ELSE i.Amount-COALESCE(a.Paid,0) END) AS decimal(28,2)) Outstanding
              FROM finance.Invoices i OUTER APPLY (SELECT CAST(SUM(CASE WHEN p.IsReversal=1 THEN -a.Amount ELSE a.Amount END) AS decimal(28,2)) Paid FROM finance.Allocations a
                JOIN finance.PaymentEvents p ON p.PaymentId=a.PaymentId WHERE a.InvoiceId=i.Id AND (p.PostedAtUtc<@At OR (@Inclusive=1 AND p.PostedAtUtc=@At))) a
              WHERE i.IssuedAtUtc<@At OR (@Inclusive=1 AND i.IssuedAtUtc=@At) GROUP BY i.Direction)
            SELECT d.Direction,COALESCE(i.Invoiced,0) Invoiced,COALESCE(i.Credits,0) Credits,
              COALESCE(p.Paid,0) Paid,COALESCE(p.Refunded,0) Refunded,
              COALESCE(i.Invoiced,0)-COALESCE(i.Credits,0)-COALESCE(p.Paid,0)+COALESCE(p.Refunded,0) Balance,
              COALESCE(a.Outstanding,0) Outstanding,
              COALESCE(i.Invoiced,0)-COALESCE(i.Credits,0)-COALESCE(p.Paid,0)+COALESCE(p.Refunded,0)-COALESCE(a.Outstanding,0) Difference
            FROM D d LEFT JOIN I i ON i.Direction=d.Direction LEFT JOIN P p ON p.Direction=d.Direction LEFT JOIN A a ON a.Direction=d.Direction
            """, Parameters(cutoff),transaction:transaction,cancellationToken: ct);
        var result=(await connection.QueryAsync<ReconciliationRow>(command)).ToArray();
        if(ownedTransaction is not null)await ownedTransaction.CommitAsync(ct);
        return result;
    }

    public async Task<ReconciliationExceptionPage> ExceptionsAsync(DateTime? asOf,DateOnly? asOfDate,int page,int size,CancellationToken ct,int? orderId=null,FinanceDirection? direction=null,bool includeSourceCosts=false)
    {
        FinanceService.ValidateDirection(direction);
        if(page<1 || page>100_000 || size<1 || size>100 || orderId is <=0)
            throw ErpException.Invalid("invalid_reconciliation_page","Bộ lọc hoặc phân trang đối soát không hợp lệ.");
        var cutoff=Cutoff(asOf,asOfDate);
        await using var ownedTransaction=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct):null;
        var connection=db.Database.GetDbConnection();
        var transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        RequireConsistentTransaction(transaction.IsolationLevel);
        var parameters=Parameters(cutoff);
        parameters.Add("OrderId",orderId,DbType.Int32); parameters.Add("Direction",(byte?)direction,DbType.Byte);
        parameters.Add("IncludeSourceCosts",includeSourceCosts,DbType.Boolean);
        parameters.Add("Offset",(page-1)*size,DbType.Int32); parameters.Add("Size",size,DbType.Int32);
        var command=new CommandDefinition(ExceptionSql+"""
            SELECT * INTO #FinanceExceptions FROM Filtered;
            SELECT COUNT(*) FROM #FinanceExceptions;
            SELECT c.Code,c.Entity,c.EntityId,c.OrderId,c.StockDocumentId,c.InvoiceId,c.PaymentId,c.Expected,c.Actual,
              CAST(c.Actual AS decimal(28,2))-CAST(c.Expected AS decimal(28,2)) Difference,c.Message,c.ExpectedReference,c.ActualReference,
              o.Kind SourceOrderKind,i.Direction InvoiceDirection
            FROM #FinanceExceptions c LEFT JOIN orders.Orders o ON o.Id=c.OrderId LEFT JOIN finance.Invoices i ON i.Id=c.InvoiceId
            ORDER BY c.Code,c.Entity,c.EntityId,c.InvoiceId,c.PaymentId OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY;
            DROP TABLE #FinanceExceptions;
            """,parameters,transaction:transaction,cancellationToken:ct);
        using var results=await connection.QueryMultipleAsync(command);
        var count=await results.ReadSingleAsync<int>();
        var rows=(await results.ReadAsync<ReconciliationExceptionRow>()).AsList();
        if(ownedTransaction is not null)await ownedTransaction.CommitAsync(ct);
        return new(rows,page,size,count,cutoff);
    }

    // These are source integrity checks, not another rearrangement of invoice totals.
    // Presence uses immutable current links so intra-posting timestamp differences do not create false missing invoices.
    private const string ExceptionSql="""
        WITH F AS (SELECT * FROM orders.Fulfillments WHERE CreatedAtUtc<@At OR (@Inclusive=1 AND CreatedAtUtc=@At)),
        I AS (SELECT * FROM finance.Invoices WHERE IssuedAtUtc<@At OR (@Inclusive=1 AND IssuedAtUtc=@At)),
        P AS (SELECT * FROM finance.Payments WHERE PostedAtUtc<@At OR (@Inclusive=1 AND PostedAtUtc=@At)),
        A AS (SELECT a.* FROM finance.Allocations a JOIN P p ON p.Id=a.PaymentId),
        R AS (SELECT * FROM finance.PaymentReversals WHERE PostedAtUtc<@At OR (@Inclusive=1 AND PostedAtUtc=@At)),
        ActiveA AS (SELECT a.* FROM A a WHERE NOT EXISTS(SELECT 1 FROM R r WHERE r.PaymentId=a.PaymentId)),
        CashP AS (SELECT p.* FROM P p WHERE NOT EXISTS(SELECT 1 FROM R r WHERE r.PaymentId=p.Id)
          AND p.Amount=(SELECT SUM(a.Amount) FROM finance.Allocations a WHERE a.PaymentId=p.Id)
          AND NOT EXISTS(SELECT 1 FROM finance.Allocations a JOIN finance.Invoices i ON i.Id=a.InvoiceId WHERE a.PaymentId=p.Id
            AND (i.PartnerId<>p.PartnerId OR i.Direction<>p.Direction OR i.IsCredit<>p.IsRefund))),
        Checks AS (
          SELECT CAST('missing_fulfillment_invoice' AS varchar(64)) Code,CAST('Fulfillment' AS varchar(32)) Entity,
            CONVERT(nvarchar(64),f.Id) EntityId,f.OrderId,f.StockDocumentId,CAST(NULL AS bigint) InvoiceId,CAST(NULL AS bigint) PaymentId,
            CAST(f.Amount AS decimal(19,2)) Expected,CAST(NULL AS decimal(19,2)) Actual,
            CAST(N'Phiếu thực hiện chưa có hóa đơn liên kết.' AS nvarchar(250)) Message,
            CAST(NULL AS nvarchar(128)) ExpectedReference,CAST(NULL AS nvarchar(128)) ActualReference,CAST(NULL AS tinyint) AttributedDirection
          FROM F f WHERE NOT EXISTS(SELECT 1 FROM finance.Invoices i WHERE i.StockDocumentId=f.StockDocumentId)
          UNION ALL
          SELECT 'invoice_source_missing','Invoice',CONVERT(nvarchar(64),i.Id),i.OrderId,i.StockDocumentId,i.Id,NULL,NULL,i.Amount,
            N'Hóa đơn không có phiếu thực hiện nguồn.',NULL,NULL,i.Direction FROM I i
          WHERE NOT EXISTS(SELECT 1 FROM orders.Fulfillments f WHERE f.StockDocumentId=i.StockDocumentId)
          UNION ALL
          SELECT v.Code,'Invoice',CONVERT(nvarchar(64),i.Id),f.OrderId,i.StockDocumentId,i.Id,NULL,v.Expected,v.Actual,v.Message,NULL,NULL,i.Direction
          FROM I i JOIN orders.Fulfillments f ON f.StockDocumentId=i.StockDocumentId JOIN orders.Orders o ON o.Id=f.OrderId
          CROSS APPLY(VALUES
            ('invoice_source_amount',CAST(f.Amount AS decimal(19,2)),CAST(i.Amount AS decimal(19,2)),N'Giá trị hóa đơn khác phiếu thực hiện nguồn.'),
            ('invoice_order_scope',CAST(f.OrderId AS decimal(19,2)),CAST(i.OrderId AS decimal(19,2)),N'Hóa đơn liên kết sai đơn hàng.'),
            ('invoice_direction_scope',CAST(CASE WHEN o.Kind=2 THEN 1 ELSE 2 END AS decimal(19,2)),CAST(i.Direction AS decimal(19,2)),N'Loại công nợ khác loại đơn hàng nguồn.'),
            ('invoice_credit_scope',CAST(f.IsReturn AS decimal(19,2)),CAST(i.IsCredit AS decimal(19,2)),N'Loại hóa đơn hoặc ghi giảm khác phiếu nguồn.')
          )v(Code,Expected,Actual,Message) WHERE v.Expected<>v.Actual
          UNION ALL
          SELECT 'invoice_source_cost','Invoice',CONVERT(nvarchar(64),i.Id),f.OrderId,i.StockDocumentId,i.Id,NULL,s.Cost,i.CostAmount,
            N'Giá vốn hóa đơn khác tổng giá vốn đã ghi nhận trên các dòng chứng từ nguồn.',NULL,NULL,i.Direction
          FROM I i JOIN orders.Fulfillments f ON f.StockDocumentId=i.StockDocumentId JOIN orders.Orders o ON o.Id=f.OrderId AND o.Kind=2
          CROSS APPLY(SELECT SUM(l.PostedSalesCost) Cost FROM inventory.DocumentLines l WHERE l.DocumentId=i.StockDocumentId
            HAVING COUNT(*)>0 AND COUNT(l.PostedSalesCost)=COUNT(*))s
          WHERE @IncludeSourceCosts=1 AND s.Cost<>i.CostAmount
          UNION ALL
          SELECT 'invoice_partner_scope','Invoice',CONVERT(nvarchar(64),i.Id),f.OrderId,i.StockDocumentId,i.Id,NULL,NULL,NULL,
            N'Đối tác hóa đơn khác đối tác đơn hàng nguồn.',CONVERT(nvarchar(128),o.PartnerId),CONVERT(nvarchar(128),i.PartnerId),i.Direction
          FROM I i JOIN orders.Fulfillments f ON f.StockDocumentId=i.StockDocumentId JOIN orders.Orders o ON o.Id=f.OrderId WHERE i.PartnerId<>o.PartnerId
          UNION ALL
          SELECT 'invoice_stock_state','Invoice',CONVERT(nvarchar(64),i.Id),i.OrderId,i.StockDocumentId,i.Id,NULL,2,CAST(d.Status AS decimal(19,2)),
            N'Chứng từ kho nguồn chưa ghi sổ hoặc đã hủy; trạng thái hợp lệ là đã ghi sổ hoặc đã đảo.',NULL,NULL,i.Direction
          FROM I i JOIN inventory.Documents d ON d.Id=i.StockDocumentId WHERE d.Status NOT IN(2,4)
          UNION ALL
          SELECT 'payment_allocation_total','Payment',CONVERT(nvarchar(64),p.Id),s.OrderId,NULL,NULL,p.Id,p.Amount,COALESCE(s.Amount,0),
            N'Tổng phân bổ khác giá trị phiếu thu hoặc chi.',NULL,NULL,p.Direction FROM P p
          OUTER APPLY(SELECT SUM(a.Amount) Amount,MIN(i.OrderId) OrderId FROM finance.Allocations a JOIN finance.Invoices i ON i.Id=a.InvoiceId WHERE a.PaymentId=p.Id)s
          WHERE p.Amount<>COALESCE(s.Amount,0)
          UNION ALL
          SELECT 'invoice_overallocated','Invoice',CONVERT(nvarchar(64),i.Id),i.OrderId,i.StockDocumentId,i.Id,NULL,i.Amount,s.Amount,
            N'Tổng tiền phân bổ vượt giá trị hóa đơn.',NULL,NULL,i.Direction FROM I i
          CROSS APPLY(SELECT SUM(a.Amount) Amount FROM ActiveA a WHERE a.InvoiceId=i.Id)s WHERE s.Amount>i.Amount
          UNION ALL
          SELECT 'reversal_before_payment','PaymentReversal',CONVERT(nvarchar(64),r.Id),NULL,NULL,NULL,p.Id,NULL,NULL,
            N'Thời điểm đảo sớm hơn phiếu thanh toán gốc.',CONVERT(nvarchar(40),p.PostedAtUtc,126),CONVERT(nvarchar(40),r.PostedAtUtc,126),p.Direction
          FROM R r JOIN finance.Payments p ON p.Id=r.PaymentId WHERE r.PostedAtUtc<p.PostedAtUtc
          UNION ALL
          SELECT v.Code,'Allocation',CONCAT(p.Id,N'/',i.Id),i.OrderId,i.StockDocumentId,i.Id,p.Id,v.Expected,v.Actual,v.Message,NULL,NULL,p.Direction
          FROM A a JOIN P p ON p.Id=a.PaymentId JOIN finance.Invoices i ON i.Id=a.InvoiceId
          CROSS APPLY(VALUES
            ('allocation_direction_scope',CAST(i.Direction AS decimal(19,2)),CAST(p.Direction AS decimal(19,2)),N'Phân bổ sai loại công nợ.'),
            ('allocation_refund_scope',CAST(i.IsCredit AS decimal(19,2)),CAST(p.IsRefund AS decimal(19,2)),N'Phân bổ thu chi hoặc hoàn tiền không khớp loại hóa đơn.')
          )v(Code,Expected,Actual,Message) WHERE v.Expected<>v.Actual
          UNION ALL
          SELECT 'allocation_partner_scope','Allocation',CONCAT(p.Id,N'/',i.Id),i.OrderId,i.StockDocumentId,i.Id,p.Id,NULL,NULL,
            N'Đối tác phiếu thu chi khác đối tác hóa đơn.',CONVERT(nvarchar(128),i.PartnerId),CONVERT(nvarchar(128),p.PartnerId),p.Direction
          FROM A a JOIN P p ON p.Id=a.PaymentId JOIN finance.Invoices i ON i.Id=a.InvoiceId WHERE p.PartnerId<>i.PartnerId
          UNION ALL
          SELECT 'order_cash_overrefunded','Order',CONVERT(nvarchar(64),i.OrderId),i.OrderId,NULL,NULL,NULL,0,
            SUM(a.Amount*CASE WHEN p.IsRefund=1 THEN -1 ELSE 1 END),N'Tiền hoàn trả vượt tiền thực tế đã thu hoặc chi cho đơn.',NULL,NULL,i.Direction
          FROM A a JOIN CashP p ON p.Id=a.PaymentId JOIN finance.Invoices i ON i.Id=a.InvoiceId
          GROUP BY i.OrderId,i.Direction HAVING SUM(a.Amount*CASE WHEN p.IsRefund=1 THEN -1 ELSE 1 END)<0
        ),Filtered AS(SELECT c.* FROM Checks c LEFT JOIN orders.Orders o ON o.Id=c.OrderId
          WHERE (@OrderId IS NULL OR c.OrderId=@OrderId OR c.PaymentId IN
            (SELECT a.PaymentId FROM finance.Allocations a JOIN finance.Invoices i ON i.Id=a.InvoiceId WHERE i.OrderId=@OrderId))
          AND (@Direction IS NULL OR COALESCE(c.AttributedDirection,CAST(CASE WHEN o.Kind=2 THEN 1 ELSE 2 END AS tinyint))=@Direction))
        """;

    private static DynamicParameters Parameters(FinanceCutoff cutoff)
    {
        var parameters=new DynamicParameters();
        parameters.Add("At",cutoff.AtUtc,DbType.DateTime2,scale:7);
        parameters.Add("Inclusive",cutoff.Inclusive,DbType.Boolean);
        return parameters;
    }

    private static void RequireConsistentTransaction(IsolationLevel isolation)
    {
        if(isolation is not (IsolationLevel.Serializable or IsolationLevel.Snapshot))
            throw new InvalidOperationException("Finance reports require a Serializable or Snapshot read transaction.");
    }

    private static DateOnly DueDate(DateTime utc)
    {
        try{return BusinessCalendar.DateAt(DateTime.SpecifyKind(utc,DateTimeKind.Utc));}
        // A valid SQL datetime2 at the maximum value is still future-due for every supported reporting day.
        catch(ArgumentOutOfRangeException)when(utc.Year==9999){return DateOnly.MaxValue;}
    }

    public FinanceCutoff Cutoff(DateTime? at,DateOnly? day)
    {
        if(at.HasValue && day.HasValue)throw ErpException.Invalid("conflicting_cutoff","Chỉ chọn ngày nghiệp vụ hoặc thời điểm UTC, không chọn cả hai.");
        if(day.HasValue)
        {
            if(day.Value.Year<1753 || day==DateOnly.MaxValue)throw ErpException.Invalid("invalid_report_date","Ngày báo cáo ngoài phạm vi cho phép.");
            return new(BusinessCalendar.StartUtc(day.Value.AddDays(1)),false,day.Value);
        }
        if (at.HasValue && (at.Value.Kind != DateTimeKind.Utc || at.Value.Year<1753))
            throw ErpException.Invalid("utc_required", "Ngày báo cáo cần có múi giờ UTC (Z).");
        var instant=at??clock.GetUtcNow().UtcDateTime;
        try{return new(instant,true,BusinessCalendar.DateAt(instant));}
        catch(ArgumentOutOfRangeException){throw ErpException.Invalid("invalid_report_date","Ngày báo cáo ngoài phạm vi cho phép.");}
    }
}
