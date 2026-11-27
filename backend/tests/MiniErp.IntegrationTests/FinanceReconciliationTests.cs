using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Finance;
using MiniErp.Application.Auth;
using MiniErp.Application.Reporting;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Reporting;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class FinanceReconciliationTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Diagnostics_detect_missing_zero_invoice_and_wrong_source_value_while_legacy_difference_is_zero()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var missing=await SourceAsync(db,0m);
        var wrong=await SourceAsync(db,100m);
        db.Add(Invoice(wrong,110m)); await db.SaveChangesAsync();
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var missingRows=await report.ExceptionsAsync(null,null,1,10,CancellationToken.None,missing.Order.Id);
        var missingRow=Assert.Single(missingRows.Items);
        Assert.Equal("missing_fulfillment_invoice",missingRow.Code); Assert.Equal(0m,missingRow.Expected); Assert.Null(missingRow.Actual);
        Assert.Empty((await report.ExceptionsAsync(null,null,1,10,CancellationToken.None,missing.Order.Id,FinanceDirection.Payable)).Items);
        var wrongRows=await report.ExceptionsAsync(null,null,1,10,CancellationToken.None,wrong.Order.Id);
        var mismatch=Assert.Single(wrongRows.Items);
        Assert.Equal("invoice_source_amount",mismatch.Code); Assert.Equal(100m,mismatch.Expected); Assert.Equal(110m,mismatch.Actual);
        Assert.Equal(10m,mismatch.Difference); Assert.Equal(wrong.Document.Id,mismatch.StockDocumentId);
        Assert.All(await report.ReconciliationAsync(null,CancellationToken.None),row=>Assert.Equal(0m,row.Difference));
        await transaction.RollbackAsync(); db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(x=>x.Id==missing.Order.Id || x.Id==wrong.Order.Id));
    }

    [SqlServerFact]
    public async Task Diagnostics_detect_allocation_total_scope_overallocation_and_cash_without_modifying_records()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var source=await SourceAsync(db,100m);
        var invoice=Invoice(source,100m); db.Add(invoice); await db.SaveChangesAsync();
        var other=new BusinessPartner{Kind=PartnerKind.Customer,Code=Guid.NewGuid().ToString("N")[..12],Name="Diagnostic other partner"}; db.Add(other);
        db.Add(new FinancePayment{RequestId=Guid.NewGuid(),PartnerId=other.Id,Direction=FinanceDirection.Payable,IsRefund=true,
            PaymentMethodId=source.Method.Id,Amount=30m,PostedAtUtc=DateTime.UtcNow,Allocations=[new(){InvoiceId=invoice.Id,Amount=40m}]});
        var at=DateTime.UtcNow;
        var returned=new StockDocument{RequestId=Guid.NewGuid(),Kind=StockKind.Receipt,WarehouseId=source.Order.WarehouseId,CreatedAtUtc=at};
        returned.Post(at); db.Add(returned); await db.SaveChangesAsync();
        db.Add(new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=source.Order.Id,StockDocumentId=returned.Id,IsReturn=true,Amount=100m,CreatedAtUtc=at});
        var credit=new FinanceInvoice{StockDocumentId=returned.Id,OrderId=source.Order.Id,OrderNumber="DIAG-"+source.Order.Id,
            PartnerId=source.Order.PartnerId,Direction=FinanceDirection.Receivable,IsCredit=true,Amount=100m,IssuedAtUtc=at,DueAtUtc=at};
        db.Add(credit); await db.SaveChangesAsync();
        db.Add(new FinancePayment{RequestId=Guid.NewGuid(),PartnerId=source.Order.PartnerId,Direction=FinanceDirection.Receivable,IsRefund=true,
            PaymentMethodId=source.Method.Id,Amount=40m,PostedAtUtc=at,Allocations=[new(){InvoiceId=credit.Id,Amount=40m}]});
        var over=await SourceAsync(db,10m); var overInvoice=Invoice(over,10m); db.Add(overInvoice); await db.SaveChangesAsync();
        db.Add(new FinancePayment{RequestId=Guid.NewGuid(),PartnerId=over.Order.PartnerId,Direction=FinanceDirection.Receivable,
            PaymentMethodId=over.Method.Id,Amount=20m,PostedAtUtc=DateTime.UtcNow,Allocations=[new(){InvoiceId=overInvoice.Id,Amount=20m}]});
        await db.SaveChangesAsync();
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var rows=await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,source.Order.Id);
        foreach(var code in new[]{"payment_allocation_total","allocation_partner_scope","allocation_direction_scope","allocation_refund_scope","order_cash_overrefunded"})
            Assert.Contains(rows.Items,row=>row.Code==code);
        var payable=await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,source.Order.Id,FinanceDirection.Payable);
        Assert.Contains(payable.Items,row=>row.Code=="payment_allocation_total");
        Assert.Contains(payable.Items,row=>row.Code=="allocation_direction_scope");
        var directionMismatch=Assert.Single(payable.Items,row=>row.Code=="allocation_direction_scope");
        Assert.Equal(OrderKind.Sales,directionMismatch.SourceOrderKind);
        Assert.Equal(FinanceDirection.Receivable,directionMismatch.InvoiceDirection);
        Assert.Equal(source.Order.Id,directionMismatch.OrderId); Assert.Equal(invoice.Id,directionMismatch.InvoiceId);
        Assert.DoesNotContain(payable.Items,row=>row.Code=="order_cash_overrefunded");
        var receivable=await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,source.Order.Id,FinanceDirection.Receivable);
        Assert.Contains(receivable.Items,row=>row.Code=="order_cash_overrefunded");
        Assert.DoesNotContain(receivable.Items,row=>row.Code=="allocation_direction_scope");
        var partner=Assert.Single(rows.Items,row=>row.Code=="allocation_partner_scope");
        Assert.Equal(source.Order.PartnerId,Guid.Parse(partner.ExpectedReference!)); Assert.Equal(other.Id,Guid.Parse(partner.ActualReference!));
        var first=await report.ExceptionsAsync(null,null,1,1,CancellationToken.None,source.Order.Id);
        var second=await report.ExceptionsAsync(null,null,2,1,CancellationToken.None,source.Order.Id);
        Assert.Equal(rows.Total,first.Total); Assert.Equal(rows.Total,second.Total);
        Assert.NotEqual(Assert.Single(first.Items).Code,Assert.Single(second.Items).Code);
        var overRows=await report.ExceptionsAsync(null,null,1,10,CancellationToken.None,over.Order.Id);
        var overRow=Assert.Single(overRows.Items); Assert.Equal("invoice_overallocated",overRow.Code);
        Assert.Equal(10m,overRow.Expected); Assert.Equal(20m,overRow.Actual);
        Assert.Equal(30m,await db.Set<FinancePayment>().Where(p=>p.PartnerId==other.Id).Select(p=>p.Amount).SingleAsync());
        await transaction.RollbackAsync(); db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(x=>x.Id==source.Order.Id || x.Id==over.Order.Id));
    }

    [SqlServerFact]
    public async Task Zero_invoices_posted_returns_and_reversed_sources_have_no_false_source_exceptions()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var zero=await SourceAsync(db,0m); db.Add(Invoice(zero,0m));
        var source=await SourceAsync(db,100m); db.Add(Invoice(source,100m)); await db.SaveChangesAsync();
        var at=DateTime.UtcNow;
        var returned=new StockDocument{RequestId=Guid.NewGuid(),Kind=StockKind.Receipt,WarehouseId=source.Order.WarehouseId,CreatedAtUtc=at};
        returned.Post(at); db.Add(returned); await db.SaveChangesAsync();
        var returnFulfillment=new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=source.Order.Id,StockDocumentId=returned.Id,IsReturn=true,Amount=40m,CreatedAtUtc=at};
        db.Add(returnFulfillment); db.Add(new FinanceInvoice{StockDocumentId=returned.Id,OrderId=source.Order.Id,OrderNumber="DIAG-"+source.Order.Id,
            PartnerId=source.Order.PartnerId,Direction=FinanceDirection.Receivable,IsCredit=true,Amount=40m,IssuedAtUtc=at,DueAtUtc=at});
        await db.SaveChangesAsync(); returned.MarkReversed();
        var reversed=new StockDocument{RequestId=Guid.NewGuid(),Kind=StockKind.Reversal,ReversesId=returned.Id,WarehouseId=source.Order.WarehouseId,CreatedAtUtc=at};
        reversed.Post(at); db.Add(reversed); await db.SaveChangesAsync();
        db.Add(new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=source.Order.Id,StockDocumentId=reversed.Id,ReversesId=returnFulfillment.Id,IsReturn=false,Amount=40m,CreatedAtUtc=at});
        db.Add(new FinanceInvoice{StockDocumentId=reversed.Id,OrderId=source.Order.Id,OrderNumber="DIAG-"+source.Order.Id,PartnerId=source.Order.PartnerId,
            Direction=FinanceDirection.Receivable,Amount=40m,IssuedAtUtc=at,DueAtUtc=at}); await db.SaveChangesAsync();
        var product=await ProductAsync(db);
        foreach(var document in new[]{zero.Document,source.Document,returned,reversed})document.Lines.Add(CostLine(product,0m));
        await db.SaveChangesAsync();
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        Assert.Empty((await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,zero.Order.Id,includeSourceCosts:true)).Items);
        Assert.Empty((await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,source.Order.Id,includeSourceCosts:true)).Items);
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Source_cost_exception_and_its_pagination_require_sensitive_access_and_skip_incomplete_legacy_lines()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var a=await ProductAsync(db); var b=await ProductAsync(db);
        var wrong=await SourceAsync(db,100m);
        wrong.Document.Lines.Add(CostLine(a,3.01m)); wrong.Document.Lines.Add(CostLine(b,0m));
        db.Add(Invoice(wrong,101m,cost:5.03m));
        var legacy=await SourceAsync(db,0m);
        legacy.Document.Lines.Add(CostLine(a,3m)); legacy.Document.Lines.Add(CostLine(b,null));
        db.Add(Invoice(legacy,0m,cost:5m));
        var purchase=await SourceAsync(db,0m,kind:OrderKind.Purchase);
        purchase.Document.Lines.Add(CostLine(a,3m)); db.Add(Invoice(purchase,0m,cost:5m));
        await db.SaveChangesAsync();
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var publicRows=await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,wrong.Order.Id);
        Assert.Equal(1,publicRows.Total); Assert.Equal("invoice_source_amount",Assert.Single(publicRows.Items).Code);
        var sensitive=await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,wrong.Order.Id,includeSourceCosts:true);
        Assert.Equal(2,sensitive.Total);
        var cost=Assert.Single(sensitive.Items,row=>row.Code=="invoice_source_cost");
        Assert.Equal(3.01m,cost.Expected); Assert.Equal(5.03m,cost.Actual); Assert.Equal(2.02m,cost.Difference);
        var publicPage=await report.ExceptionsAsync(null,null,2,1,CancellationToken.None,wrong.Order.Id);
        Assert.Equal(1,publicPage.Total); Assert.Empty(publicPage.Items);
        var sensitivePage=await report.ExceptionsAsync(null,null,2,1,CancellationToken.None,wrong.Order.Id,includeSourceCosts:true);
        Assert.Equal(2,sensitivePage.Total); Assert.Equal("invoice_source_cost",Assert.Single(sensitivePage.Items).Code);
        Assert.Empty((await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,legacy.Order.Id,includeSourceCosts:true)).Items);
        Assert.Empty((await report.ExceptionsAsync(null,null,1,100,CancellationToken.None,purchase.Order.Id,includeSourceCosts:true)).Items);
        await transaction.RollbackAsync(); db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(order=>order.Id==wrong.Order.Id || order.Id==legacy.Order.Id || order.Id==purchase.Order.Id));
    }

    [SqlServerFact]
    public async Task Reconciliation_and_dashboard_preserve_one_cent_debt_after_receipts_credits_and_refunds()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var dashboard=scope.ServiceProvider.GetRequiredService<OperationsReport>();
        var day=new DateOnly(2042,6,15); var at=BusinessCalendar.StartUtc(day).AddHours(12);
        var grants=new HashSet<string>{Permissions.ViewFinance,Permissions.ViewReports};
        var before=await report.ReconciliationAsync(null,CancellationToken.None,day);
        var metricsBefore=await dashboard.DashboardAsync(day,day,grants,CancellationToken.None);
        Source? sale=null,purchase=null; StockDocument? returned=null;
        try
        {
            await using(var setup=await db.Database.BeginTransactionAsync())
            {
                sale=await SourceAsync(db,0.03m,at); var invoice=Invoice(sale,0.03m); db.Add(invoice);
                purchase=await SourceAsync(db,0.01m,at,OrderKind.Purchase); db.Add(Invoice(purchase,0.01m)); await db.SaveChangesAsync();
                db.Add(new FinancePayment{RequestId=Guid.NewGuid(),PartnerId=sale.Order.PartnerId,Direction=FinanceDirection.Receivable,
                    PaymentMethodId=sale.Method.Id,Amount=0.03m,PostedAtUtc=at.AddSeconds(1),Allocations=[new(){InvoiceId=invoice.Id,Amount=0.03m}]});
                returned=new StockDocument{RequestId=Guid.NewGuid(),Kind=StockKind.Receipt,WarehouseId=sale.Order.WarehouseId,CreatedAtUtc=at.AddSeconds(2)};
                returned.Post(at.AddSeconds(2)); db.Add(returned); await db.SaveChangesAsync();
                db.Add(new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=sale.Order.Id,StockDocumentId=returned.Id,IsReturn=true,Amount=0.02m,CreatedAtUtc=at.AddSeconds(2)});
                var credit=new FinanceInvoice{StockDocumentId=returned.Id,OrderId=sale.Order.Id,OrderNumber="DIAG-"+sale.Order.Id,PartnerId=sale.Order.PartnerId,
                    Direction=FinanceDirection.Receivable,IsCredit=true,Amount=0.02m,IssuedAtUtc=at.AddSeconds(2),DueAtUtc=at.AddSeconds(2)};
                db.Add(credit); await db.SaveChangesAsync();
                db.Add(new FinancePayment{RequestId=Guid.NewGuid(),PartnerId=sale.Order.PartnerId,Direction=FinanceDirection.Receivable,IsRefund=true,
                    PaymentMethodId=sale.Method.Id,Amount=0.01m,PostedAtUtc=at.AddSeconds(3),Allocations=[new(){InvoiceId=credit.Id,Amount=0.01m}]});
                await db.SaveChangesAsync(); await setup.CommitAsync();
            }
            var after=await report.ReconciliationAsync(null,CancellationToken.None,day);
            var oldAr=before.Single(x=>x.Direction==FinanceDirection.Receivable); var ar=after.Single(x=>x.Direction==FinanceDirection.Receivable);
            Assert.Equal(0.03m,ar.Invoiced-oldAr.Invoiced); Assert.Equal(0.02m,ar.Credits-oldAr.Credits);
            Assert.Equal(0.03m,ar.Paid-oldAr.Paid); Assert.Equal(0.01m,ar.Refunded-oldAr.Refunded);
            Assert.Equal(-0.01m,ar.Balance-oldAr.Balance); Assert.Equal(-0.01m,ar.Outstanding-oldAr.Outstanding); Assert.Equal(oldAr.Difference,ar.Difference);
            var oldAp=before.Single(x=>x.Direction==FinanceDirection.Payable); var ap=after.Single(x=>x.Direction==FinanceDirection.Payable);
            Assert.Equal(0.01m,ap.Balance-oldAp.Balance); Assert.Equal(0.01m,ap.Outstanding-oldAp.Outstanding);
            var metricsAfter=await dashboard.DashboardAsync(day,day,grants,CancellationToken.None);
            decimal MetricDelta(string key)=>metricsAfter.Metrics.Single(x=>x.Key==key).Value-metricsBefore.Metrics.Single(x=>x.Key==key).Value;
            Assert.Equal(-0.01m,MetricDelta("receivable")); Assert.Equal(0.01m,MetricDelta("payable"));
        }
        finally
        {
            if(sale is not null && purchase is not null && returned is not null)
            {
                // The dashboard uses a separate connection: its committed fixtures are removed atomically by their generated IDs only.
                await using var cleanup=await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM finance.Allocations WHERE PaymentId IN(SELECT Id FROM finance.Payments WHERE PartnerId={sale.Order.PartnerId} OR PartnerId={purchase.Order.PartnerId});
                    DELETE FROM finance.Payments WHERE PartnerId={sale.Order.PartnerId} OR PartnerId={purchase.Order.PartnerId};
                    DELETE FROM finance.Invoices WHERE OrderId={sale.Order.Id} OR OrderId={purchase.Order.Id};
                    DELETE FROM orders.Fulfillments WHERE OrderId={sale.Order.Id} OR OrderId={purchase.Order.Id};
                    DELETE FROM inventory.Documents WHERE Id={sale.Document.Id} OR Id={purchase.Document.Id} OR Id={returned.Id};
                    DELETE FROM orders.Orders WHERE Id={sale.Order.Id} OR Id={purchase.Order.Id};
                    DELETE FROM catalog.BusinessPartners WHERE Id={sale.Order.PartnerId} OR Id={purchase.Order.PartnerId};
                    DELETE FROM catalog.Lookups WHERE Id={sale.Order.WarehouseId} OR Id={sale.Method.Id} OR Id={purchase.Order.WarehouseId} OR Id={purchase.Method.Id};
                    """);
                await cleanup.CommitAsync();
            }
        }
    }

    [SqlServerFact]
    public async Task Business_day_includes_last_fractional_second_and_excludes_next_midnight_while_exact_cutoff_remains_inclusive()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var day=new DateOnly(2040,1,31); var boundary=BusinessCalendar.StartUtc(day.AddDays(1));
        var beforeDay=await report.ReconciliationAsync(null,CancellationToken.None,day);
        var beforeExact=await report.ReconciliationAsync(boundary,CancellationToken.None);
        var inside=await SourceAsync(db,1m,boundary.AddTicks(-1)); db.Add(Invoice(inside,1m));
        var outside=await SourceAsync(db,2m,boundary); db.Add(Invoice(outside,2m)); await db.SaveChangesAsync();
        var afterDay=await report.ReconciliationAsync(null,CancellationToken.None,day);
        var afterExact=await report.ReconciliationAsync(boundary,CancellationToken.None);
        Assert.Equal(1m,afterDay.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced-beforeDay.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced);
        Assert.Equal(3m,afterExact.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced-beforeExact.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced);
        var aged=await report.AgingAsync(null,FinanceDirection.Receivable,CancellationToken.None,day);
        Assert.Equal(1m,Assert.Single(aged,x=>x.PartnerId==inside.Order.PartnerId).Balance);
        Assert.DoesNotContain(aged,x=>x.PartnerId==outside.Order.PartnerId);
        var metadata=await report.ExceptionsAsync(null,day,1,10,CancellationToken.None,inside.Order.Id);
        Assert.Equal(boundary,metadata.Cutoff.AtUtc); Assert.False(metadata.Cutoff.Inclusive); Assert.Equal(day,metadata.Cutoff.BusinessDate);
        await Assert.ThrowsAsync<ErpException>(()=>report.ReconciliationAsync(boundary,CancellationToken.None,day));
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Invoice_source_retry_compares_staged_and_persisted_payloads()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var source=await SourceAsync(db,100m);
        var input=new FinanceSource(source.Document.Id,source.Order.Id,"DIAG-"+source.Order.Id,source.Order.PartnerId,
            FinanceDirection.Receivable,false,100m,0m,source.Document.CreatedAtUtc);
        var posting=scope.ServiceProvider.GetRequiredService<IFinancePosting>();
        await posting.AddInvoiceAsync(input,CancellationToken.None); await posting.AddInvoiceAsync(input,CancellationToken.None);
        await Assert.ThrowsAsync<ErpException>(()=>posting.AddInvoiceAsync(input with{Amount=101m},CancellationToken.None));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await posting.AddInvoiceAsync(input,CancellationToken.None);
        await Assert.ThrowsAsync<ErpException>(()=>posting.AddInvoiceAsync(input with{CostAmount=1m},CancellationToken.None));
        Assert.Equal(1,await db.Set<FinanceInvoice>().CountAsync(x=>x.StockDocumentId==source.Document.Id));
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Exact_cutoff_preserves_ticks_within_a_millisecond_and_maximum_due_date_remains_future()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report=scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var start=BusinessCalendar.StartUtc(new DateOnly(2041,6,30)).AddHours(12).AddMilliseconds(1);
        var cutoff=start.AddTicks(2);
        var before=await report.ReconciliationAsync(cutoff,CancellationToken.None);
        var included=await SourceAsync(db,3m,start.AddTicks(1)); db.Add(Invoice(included,3m));
        var excluded=await SourceAsync(db,5m,start.AddTicks(3)); db.Add(Invoice(excluded,5m)); await db.SaveChangesAsync();
        var after=await report.ReconciliationAsync(cutoff,CancellationToken.None);
        Assert.Equal(3m,after.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced-before.Single(x=>x.Direction==FinanceDirection.Receivable).Invoiced);
        var aging=await report.AgingAsync(cutoff,FinanceDirection.Receivable,CancellationToken.None);
        Assert.Equal(3m,Assert.Single(aging,x=>x.PartnerId==included.Order.PartnerId).Balance);
        Assert.DoesNotContain(aging,x=>x.PartnerId==excluded.Order.PartnerId);
        var maximum=await SourceAsync(db,7m); db.Add(Invoice(maximum,7m,DateTime.MaxValue)); await db.SaveChangesAsync();
        var current=await report.AgingAsync(null,FinanceDirection.Receivable,CancellationToken.None);
        Assert.Equal(7m,Assert.Single(current,x=>x.PartnerId==maximum.Order.PartnerId).Current);
        await Assert.ThrowsAsync<ErpException>(()=>report.AgingAsync(DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc),null,CancellationToken.None));
        await transaction.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Invoice_page_and_count_share_one_read_view_while_another_post_waits_to_commit()
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        Source source;
        await using(var setup=await db.Database.BeginTransactionAsync())
        {
            source=await SourceAsync(db,1m); db.Add(Invoice(source,1m)); await db.SaveChangesAsync(); await setup.CommitAsync();
        }
        var gate=new CountGate(); var writerReady=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerConnection=new SqlConnectionStringBuilder(db.Database.GetConnectionString()){Pooling=false}.ConnectionString;
        var options=new DbContextOptionsBuilder<ErpDbContext>().UseSqlServer(readerConnection).AddInterceptors(gate,new LowReadPriority()).Options;
        await using var reader=new ErpDbContext(options);
        var reading=new FinanceService(reader,TimeProvider.System).InvoicesAsync(null,source.Order.PartnerId,1,100,CancellationToken.None);
        Task? writing=null; int postedDocument=0;
        try
        {
            await gate.Read.Task.WaitAsync(TimeSpan.FromSeconds(10));
            writing=Task.Run(async()=>
            {
                await using var writerScope=fixture.Services.CreateAsyncScope();
                var writer=writerScope.ServiceProvider.GetRequiredService<ErpDbContext>();
                await using var posting=await writer.Database.BeginTransactionAsync();
                await writer.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM orders.Orders WITH(UPDLOCK,HOLDLOCK) WHERE Id={source.Order.Id}");
                var at=DateTime.UtcNow;
                var document=new StockDocument{RequestId=Guid.NewGuid(),Kind=StockKind.Issue,WarehouseId=source.Order.WarehouseId,CreatedAtUtc=at};
                document.Post(at); writer.Add(document); await writer.SaveChangesAsync(); postedDocument=document.Id;
                writer.Add(new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=source.Order.Id,StockDocumentId=document.Id,Amount=2m,CreatedAtUtc=at});
                writer.Add(new FinanceInvoice{StockDocumentId=document.Id,OrderId=source.Order.Id,OrderNumber="DIAG-"+source.Order.Id,PartnerId=source.Order.PartnerId,
                    Direction=FinanceDirection.Receivable,Amount=2m,IssuedAtUtc=at,DueAtUtc=at});
                writerReady.TrySetResult(true); await writer.SaveChangesAsync(); await posting.CommitAsync();
            });
            await writerReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(200);
            Assert.False(writing.IsCompleted,"A new matching invoice must not commit between the count and the page.");
            gate.Continue.TrySetResult(true);
            var page=await reading.WaitAsync(TimeSpan.FromSeconds(10)); await writing.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(page.Total is 1 or 2); Assert.Equal(page.Total,page.Items.Count);
            Assert.Equal(page.Total==1?1m:3m,page.Items.Sum(x=>x.Amount));
            Assert.Equal(2,await db.Set<FinanceInvoice>().CountAsync(x=>x.OrderId==source.Order.Id));
        }
        finally
        {
            gate.Continue.TrySetResult(true);
            try{await reading;}catch{ /* Preserve the test assertion while releasing its read transaction. */ }
            if(writing is not null)try{await writing;}catch{ /* The posting transaction rolls itself back. */ }
            // Delete only this test's generated IDs in one cleanup transaction, never existing history.
            await using var cleanup=await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM finance.Invoices WHERE OrderId={source.Order.Id};
                DELETE FROM orders.Fulfillments WHERE OrderId={source.Order.Id};
                DELETE FROM inventory.Documents WHERE Id={source.Document.Id} OR Id={postedDocument};
                DELETE FROM orders.Orders WHERE Id={source.Order.Id};
                DELETE FROM catalog.BusinessPartners WHERE Id={source.Order.PartnerId};
                DELETE FROM catalog.Lookups WHERE Id={source.Order.WarehouseId} OR Id={source.Method.Id};
                """);
            await cleanup.CommitAsync();
        }
    }

    private sealed class CountGate:DbCommandInterceptor
    {
        private int paused;
        public TaskCompletionSource<bool> Read{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Continue{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken ct=default)
        {
            if(command.CommandText.Contains("COUNT(*)",StringComparison.Ordinal) && command.CommandText.Contains("[finance].[Invoices]",StringComparison.Ordinal) && Interlocked.Exchange(ref paused,1)==0)
            {
                Read.TrySetResult(true); await Continue.Task.WaitAsync(TimeSpan.FromSeconds(10),ct);
            }
            return result;
        }
    }

    private sealed class LowReadPriority:DbConnectionInterceptor
    {
        public override async Task ConnectionOpenedAsync(DbConnection connection,ConnectionEndEventData eventData,CancellationToken ct=default)
        {
            // Keep the writer alive if the intentionally interleaved read is deadlocked; this unpooled test session cannot leak its setting.
            await using var command=connection.CreateCommand(); command.CommandText="SET DEADLOCK_PRIORITY LOW";
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private sealed record Source(TradeOrder Order,StockDocument Document,CatalogLookup Method);
    private static async Task<Source> SourceAsync(ErpDbContext db,decimal amount,DateTime? issued=null,OrderKind kind=OrderKind.Sales)
    {
        var code=Guid.NewGuid().ToString("N")[..12]; var at=issued??DateTime.UtcNow;
        var partner=new BusinessPartner{Kind=kind==OrderKind.Sales?PartnerKind.Customer:PartnerKind.Supplier,Code=code,Name="Reconciliation "+code};
        var warehouse=new CatalogLookup{Kind=LookupKind.Warehouse,Code=code,Name=code};
        var method=new CatalogLookup{Kind=LookupKind.PaymentMethod,Code=code,Name=code};
        db.AddRange(partner,warehouse,method);
        var order=new TradeOrder{RequestId=Guid.NewGuid(),Kind=kind,PartnerId=partner.Id,WarehouseId=warehouse.Id,CreatedAtUtc=at,UpdatedAtUtc=at};
        order.Submit(); order.Approve(); order.RefreshStatus(); db.Add(order); await db.SaveChangesAsync();
        var document=new StockDocument{RequestId=Guid.NewGuid(),Kind=kind==OrderKind.Sales?StockKind.Issue:StockKind.Receipt,WarehouseId=warehouse.Id,CreatedAtUtc=at};
        document.Post(at); db.Add(document); await db.SaveChangesAsync();
        db.Add(new OrderFulfillment{RequestId=Guid.NewGuid(),OrderId=order.Id,StockDocumentId=document.Id,Amount=amount,CreatedAtUtc=at});
        await db.SaveChangesAsync(); return new(order,document,method);
    }
    private static FinanceInvoice Invoice(Source source,decimal amount,DateTime? due=null,decimal cost=0)=>new(){StockDocumentId=source.Document.Id,OrderId=source.Order.Id,
        OrderNumber="DIAG-"+source.Order.Id,PartnerId=source.Order.PartnerId,Direction=source.Order.Kind==OrderKind.Sales?FinanceDirection.Receivable:FinanceDirection.Payable,
        Amount=amount,CostAmount=cost,IssuedAtUtc=source.Document.CreatedAtUtc,DueAtUtc=due??source.Document.CreatedAtUtc};
    private static async Task<Product> ProductAsync(ErpDbContext db)
    {
        var code=Guid.NewGuid().ToString("N")[..12]; var at=DateTime.UtcNow;
        var category=new CatalogLookup{Kind=LookupKind.Category,Code=code,Name=code};
        var unit=new CatalogLookup{Kind=LookupKind.Unit,Code=code,Name=code}; db.AddRange(category,unit);
        var product=new Product{Sku=code,Name="Diagnostic cost "+code,CategoryId=category.Id,UnitId=unit.Id,CreatedAtUtc=at,UpdatedAtUtc=at};
        db.Add(product); await db.SaveChangesAsync(); return product;
    }
    private static StockLine CostLine(Product product,decimal? amount)
    {
        var line=new StockLine{ProductId=product.Id,Sku=product.Sku,ProductName=product.Name,Quantity=1m,UnitCost=amount};
        if(amount.HasValue)line.AttributeSalesCost(amount.Value);
        return line;
    }
}
