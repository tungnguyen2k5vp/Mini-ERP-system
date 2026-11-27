using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Auth;
using MiniErp.Application.Finance;
using MiniErp.Application.Reporting;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Reporting;

namespace MiniErp.IntegrationTests;

public sealed partial class FinanceReconciliationTests
{
    [SqlServerFact]
    public async Task Dashboard_and_statement_day_closing_restore_cash_only_on_reversal_day()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var report = scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var dashboard = scope.ServiceProvider.GetRequiredService<OperationsReport>();
        var day = new DateOnly(2046, 6, 10);
        var grants = new HashSet<string> { Permissions.ViewFinance, Permissions.ViewReports };
        decimal Receivable(MiniErp.Application.Reporting.DashboardResponse response) => response.Metrics.Single(x => x.Key == "receivable").Value;
        var beforeDay = Receivable(await dashboard.DashboardAsync(day, day, grants, default));
        var beforeNext = Receivable(await dashboard.DashboardAsync(day.AddDays(1), day.AddDays(1), grants, default));
        Source? source = null;
        try
        {
            await using (var setup = await db.Database.BeginTransactionAsync())
            {
                var at = BusinessCalendar.StartUtc(day).AddHours(1);
                source = await SourceAsync(db, 100m, at);
                var invoice = Invoice(source, 100m); db.Add(invoice); await db.SaveChangesAsync();
                var payment = new FinancePayment { RequestId = Guid.NewGuid(), PartnerId = source.Order.PartnerId,
                    Direction = FinanceDirection.Receivable, PaymentMethodId = source.Method.Id, Amount = 40m,
                    PostedAtUtc = at.AddHours(1), Allocations = [new() { InvoiceId = invoice.Id, Amount = 40m }] };
                db.Add(payment); await db.SaveChangesAsync();
                db.Add(new PaymentReversal { PaymentId = payment.Id, RequestId = Guid.NewGuid(),
                    PostedAtUtc = BusinessCalendar.StartUtc(day.AddDays(1)), Reason = "Next day correction" });
                await db.SaveChangesAsync(); await setup.CommitAsync();
            }
            foreach (var pair in new[] { (Day: day, Expected: 60m, Baseline: beforeDay), (Day: day.AddDays(1), Expected: 100m, Baseline: beforeNext) })
            {
                var statement = await report.StatementAsync(source.Order.PartnerId, null, pair.Day, 1, 100, default);
                Assert.Equal(pair.Expected, statement.ClosingBalance);
                Assert.Equal(pair.Expected, Receivable(await dashboard.DashboardAsync(pair.Day, pair.Day, grants, default)) - pair.Baseline);
                Assert.Equal(pair.Expected, Assert.Single(await report.AgingAsync(null, FinanceDirection.Receivable, default, pair.Day), x => x.PartnerId == source.Order.PartnerId).Balance);
                Assert.Empty((await report.ExceptionsAsync(null, pair.Day, 1, 100, default, source.Order.Id)).Items);
            }
        }
        finally
        {
            if (source is not null)
            {
                db.ChangeTracker.Clear();
                await using var cleanup = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    DELETE FROM finance.PaymentReversals WHERE PaymentId IN (SELECT Id FROM finance.Payments WHERE PartnerId={source.Order.PartnerId});
                    DELETE FROM finance.Allocations WHERE PaymentId IN (SELECT Id FROM finance.Payments WHERE PartnerId={source.Order.PartnerId});
                    DELETE FROM finance.Payments WHERE PartnerId={source.Order.PartnerId};
                    DELETE FROM finance.Invoices WHERE OrderId={source.Order.Id};
                    DELETE FROM orders.Fulfillments WHERE OrderId={source.Order.Id};
                    DELETE FROM inventory.Documents WHERE Id={source.Document.Id};
                    DELETE FROM orders.Orders WHERE Id={source.Order.Id};
                    DELETE FROM catalog.BusinessPartners WHERE Id={source.Order.PartnerId};
                    DELETE FROM catalog.Lookups WHERE Id={source.Order.WarehouseId} OR Id={source.Method.Id};
                    """);
                await cleanup.CommitAsync();
            }
        }
    }

    [SqlServerFact]
    public async Task Diagnostics_identify_reversal_timestamp_before_original_payment()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var source = await SourceAsync(db, 10m);
        var invoice = Invoice(source, 10m); db.Add(invoice); await db.SaveChangesAsync();
        var payment = new FinancePayment { RequestId = Guid.NewGuid(), PartnerId = source.Order.PartnerId,
            Direction = FinanceDirection.Receivable, PaymentMethodId = source.Method.Id, Amount = 5m,
            PostedAtUtc = DateTime.UtcNow, Allocations = [new() { InvoiceId = invoice.Id, Amount = 5m }] };
        db.Add(payment); await db.SaveChangesAsync();
        db.Add(new PaymentReversal { PaymentId = payment.Id, RequestId = Guid.NewGuid(), PostedAtUtc = payment.PostedAtUtc.AddTicks(-1), Reason = "Invalid chronology fixture" });
        await db.SaveChangesAsync();
        var rows = await scope.ServiceProvider.GetRequiredService<FinanceReport>().ExceptionsAsync(null, null, 1, 100, default, source.Order.Id);
        Assert.Contains(rows.Items, x => x.Code == "reversal_before_payment" && x.PaymentId == payment.Id);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Credit_exposure_keeps_fractional_cents_and_rejects_unknown_or_supplier_partners()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var source = await SourceAsync(db, 0.03m);
        var invoice = Invoice(source, 0.03m); db.Add(invoice); await db.SaveChangesAsync();
        db.Add(new FinancePayment { RequestId = Guid.NewGuid(), PartnerId = source.Order.PartnerId,
            Direction = FinanceDirection.Receivable, PaymentMethodId = source.Method.Id, Amount = 0.02m,
            PostedAtUtc = DateTime.UtcNow, Allocations = [new() { InvoiceId = invoice.Id, Amount = 0.02m }] });
        var product = await ProductAsync(db);
        var commitment = new TradeOrder { RequestId = Guid.NewGuid(), Kind = OrderKind.Sales, PartnerId = source.Order.PartnerId,
            WarehouseId = source.Order.WarehouseId, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            Lines = [new() { ProductId = product.Id, Sku = product.Sku, ProductName = product.Name, Quantity = 3, FulfilledQuantity = 1, UnitPrice = 0.005m }] };
        commitment.Submit(); commitment.Approve(); db.Add(commitment);
        (await db.Partners.SingleAsync(x => x.Id == source.Order.PartnerId)).CreditLimit = 0.02m;
        await db.SaveChangesAsync();
        var exposure = await MiniErp.Infrastructure.Orders.CustomerCredit.ForPartnerAsync(db, source.Order.PartnerId, default);
        Assert.Equal(0.01m, exposure.Outstanding); Assert.Equal(0.01m, exposure.Unbilled);
        Assert.Equal(0.02m, exposure.Used); Assert.Equal(0m, exposure.Available); Assert.False(exposure.RequiresOverride);
        var candidate = new TradeOrder { RequestId = Guid.NewGuid(), Kind = OrderKind.Sales, PartnerId = source.Order.PartnerId,
            WarehouseId = source.Order.WarehouseId, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            Lines = [new() { ProductId = product.Id, Quantity = 1, UnitPrice = 0.01m }] };
        db.Add(candidate); await db.SaveChangesAsync();
        var projected = await MiniErp.Infrastructure.Orders.CustomerCredit.ForOrderAsync(db, candidate.Id, default);
        Assert.Equal(0.03m, projected.Projected); Assert.True(projected.RequiresOverride);
        var missing = await Assert.ThrowsAsync<MiniErp.Domain.ErpException>(() => MiniErp.Infrastructure.Orders.CustomerCredit.ForPartnerAsync(db, Guid.NewGuid(), default));
        Assert.Equal("customer_missing", missing.Code);
        var supplier = await SourceAsync(db, 0, kind: OrderKind.Purchase);
        var wrongKind = await Assert.ThrowsAsync<MiniErp.Domain.ErpException>(() => MiniErp.Infrastructure.Orders.CustomerCredit.ForPartnerAsync(db, supplier.Order.PartnerId, default));
        Assert.Equal("customer_missing", wrongKind.Code);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Historical_cash_reversal_respects_exact_tick_and_statement_paging_preserves_original_allocation()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var day = new DateOnly(2046, 4, 12);
        var issued = BusinessCalendar.StartUtc(day.AddDays(-2)).AddHours(1);
        var paid = BusinessCalendar.StartUtc(day).AddHours(1);
        var corrected = paid.AddTicks(2);
        var baseline = (await report.ReconciliationAsync(paid.AddTicks(-1), default)).Single(x => x.Direction == FinanceDirection.Receivable);
        var source = await SourceAsync(db, 100m, issued);
        var invoice = Invoice(source, 100m);
        db.Add(invoice); await db.SaveChangesAsync();
        var payment = new FinancePayment { RequestId = Guid.NewGuid(), PartnerId = source.Order.PartnerId,
            Direction = FinanceDirection.Receivable, PaymentMethodId = source.Method.Id, Amount = 40m,
            PostedAtUtc = paid, Allocations = [new() { InvoiceId = invoice.Id, Amount = 40m }] };
        db.Add(payment); await db.SaveChangesAsync();
        db.Add(new PaymentReversal { PaymentId = payment.Id, RequestId = Guid.NewGuid(), PostedAtUtc = corrected, Reason = "Historical correction" });
        await db.SaveChangesAsync();
        foreach (var cutoff in new[] { paid.AddTicks(-1), paid, corrected.AddTicks(-1), corrected })
        {
            var expected = cutoff >= paid && cutoff < corrected ? 60m : 100m;
            var statement = await report.StatementAsync(source.Order.PartnerId, cutoff, null, 1, 100, default);
            Assert.Equal(expected, statement.ClosingBalance);
            Assert.Equal(expected, statement.Overdue);
            Assert.Equal(expected, statement.Items.Sum(x => x.Amount));
            var debit = Assert.Single(statement.Items, x => x.Kind == "Invoice");
            Assert.Equal(expected, debit.Outstanding); Assert.True(debit.IsOverdue);
            var aging = Assert.Single(await report.AgingAsync(cutoff, FinanceDirection.Receivable, default), x => x.PartnerId == source.Order.PartnerId);
            Assert.Equal(expected, aging.Balance); Assert.Equal(expected, aging.Days1To30);
            var totals = (await report.ReconciliationAsync(cutoff, default)).Single(x => x.Direction == FinanceDirection.Receivable);
            Assert.Equal(expected, totals.Balance - baseline.Balance);
            Assert.Equal(expected, totals.Outstanding - baseline.Outstanding);
            Assert.Equal(0m, totals.Difference);
            Assert.Empty((await report.ExceptionsAsync(cutoff, null, 1, 100, default, source.Order.Id)).Items);
        }
        var before = await report.StatementAsync(source.Order.PartnerId, corrected.AddTicks(-1), null, 1, 1, default);
        Assert.Equal(2, before.Total);
        var pages = new List<PartnerStatementRow>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await report.StatementAsync(source.Order.PartnerId, corrected, null, page, 1, default);
            Assert.Equal(3, result.Total); Assert.Equal(100m, result.ClosingBalance); Assert.Equal(100m, result.Overdue);
            pages.Add(Assert.Single(result.Items));
        }
        Assert.Equal(3, pages.Select(x => (x.Kind, x.SourceId, x.InvoiceId)).Distinct().Count());
        Assert.Equal(-40m, Assert.Single(pages, x => x.Kind == "Payment").Amount);
        Assert.Equal(40m, Assert.Single(pages, x => x.Kind == "PaymentReversal").Amount);
        Assert.All(pages.Where(x => x.PaymentId.HasValue), x => Assert.Equal(payment.Id, x.PaymentId));
        Assert.Equal(40m, await db.Set<PaymentAllocation>().Where(x => x.PaymentId == payment.Id).SumAsync(x => x.Amount));
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Historical_statement_credits_offset_oldest_debit_within_their_order_only()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var report = scope.ServiceProvider.GetRequiredService<FinanceReport>();
        var at = BusinessCalendar.StartUtc(new DateOnly(2046, 5, 1));
        var source = await SourceAsync(db, 40m, at);
        var first = Invoice(source, 40m); db.Add(first); await db.SaveChangesAsync();
        async Task<FinanceInvoice> Add(decimal amount, bool credit, TradeOrder order, DateTime stamp)
        {
            var document = new StockDocument { RequestId = Guid.NewGuid(), Kind = credit ? StockKind.Receipt : StockKind.Issue,
                WarehouseId = order.WarehouseId, CreatedAtUtc = stamp };
            document.Post(stamp); db.Add(document); await db.SaveChangesAsync();
            db.Add(new OrderFulfillment { RequestId = Guid.NewGuid(), OrderId = order.Id, StockDocumentId = document.Id,
                IsReturn = credit, Amount = amount, CreatedAtUtc = stamp });
            var result = new FinanceInvoice { StockDocumentId = document.Id, OrderId = order.Id, OrderNumber = "DIAG-" + order.Id,
                PartnerId = order.PartnerId, Direction = FinanceDirection.Receivable, IsCredit = credit, Amount = amount,
                IssuedAtUtc = stamp, DueAtUtc = stamp };
            db.Add(result); await db.SaveChangesAsync(); return result;
        }
        var second = await Add(60m, false, source.Order, at.AddTicks(1));
        var otherOrder = new TradeOrder { RequestId = Guid.NewGuid(), Kind = OrderKind.Sales, PartnerId = source.Order.PartnerId,
            WarehouseId = source.Order.WarehouseId, CreatedAtUtc = at, UpdatedAtUtc = at };
        otherOrder.Submit(); otherOrder.Approve(); db.Add(otherOrder); await db.SaveChangesAsync();
        var other = await Add(20m, false, otherOrder, at.AddTicks(2));
        await Add(50m, true, source.Order, at.AddTicks(3));
        var statement = await report.StatementAsync(source.Order.PartnerId, at.AddDays(1), null, 1, 100, default);
        Assert.Equal(70m, statement.ClosingBalance); Assert.Equal(70m, statement.Overdue);
        Assert.Equal(0m, Assert.Single(statement.Items, x => x.Kind == "Invoice" && x.InvoiceId == first.Id).Outstanding);
        Assert.Equal(50m, Assert.Single(statement.Items, x => x.Kind == "Invoice" && x.InvoiceId == second.Id).Outstanding);
        Assert.Equal(20m, Assert.Single(statement.Items, x => x.Kind == "Invoice" && x.InvoiceId == other.Id).Outstanding);
        Assert.Null(Assert.Single(statement.Items, x => x.Kind == "Credit").Outstanding);
        Assert.Empty((await report.ExceptionsAsync(at.AddDays(1), null, 1, 100, default, source.Order.Id)).Items);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Statement_permission_and_zero_event_partner_are_explicit()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var code = Guid.NewGuid().ToString("N")[..12];
        using var createdPartner = await admin.PostAsJsonAsync("/api/v1/customers",
            new MiniErp.Application.Catalog.PartnerInput(code, "Empty statement " + code, null, null, null, null, 0, 0, true, null));
        createdPartner.EnsureSuccessStatusCode();
        var partner = (await createdPartner.Content.ReadFromJsonAsync<MiniErp.Application.Catalog.PartnerResponse>())!;
        var email = $"statement-{Guid.NewGuid():N}@minierp.example";
        var password = Guid.NewGuid().ToString("N") + "Aa7!";
        using var user = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Restricted statement", password, "Sales Staff"));
        user.EnsureSuccessStatusCode();
        using var staff = await fixture.AuthenticatedClientAsync(email, password);
        var path = $"/api/v1/finance/statement?partnerId={partner.Id}&asOfDate=2046-05-01";
        using var denied = await staff.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var statement = (await admin.GetFromJsonAsync<PartnerStatementPage>(path))!;
        Assert.Empty(statement.Items); Assert.Equal(0, statement.Total);
        Assert.Equal(0m, statement.ClosingBalance); Assert.Equal(0m, statement.Overdue);
        Assert.Equal(partner.Name, statement.PartnerName);
    }
}
