using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Finance;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class PaymentCorrectionTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Past_clock_cannot_post_before_invoice_or_later_cash_and_reversal()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(FinanceDirection.Receivable);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var issued = await db.Set<FinanceInvoice>().Where(x => x.Id == s.Debits[0]).Select(x => x.IssuedAtUtc).SingleAsync();
        var actor = new RequestActor(Guid.NewGuid(), "past-cash");
        var input = new PaymentInput(Guid.NewGuid(), s.Partner, FinanceDirection.Receivable, false, s.Method, 10, null, [new(s.Debits[0], 10)]);
        var past = new FinanceService(db, new FixedClock(new DateTimeOffset(issued.AddTicks(-1), TimeSpan.Zero)));
        Assert.Equal("backdated_settlement", (await Assert.ThrowsAsync<ErpException>(() => past.PayAsync(input, actor, default))).Code);
        var paid = await PayAsync(client, input);
        var beforeCash = new FinanceService(db, new FixedClock(new DateTimeOffset(paid.PostedAtUtc.AddTicks(-1), TimeSpan.Zero)));
        Assert.Equal("backdated_settlement", (await Assert.ThrowsAsync<ErpException>(() => beforeCash.PayAsync(input with { RequestId = Guid.NewGuid() }, actor, default))).Code);
        using var reversedResponse = await client.PostAsJsonAsync($"/api/v1/finance/payments/{paid.Id}/reverse", new PaymentReversalInput(Guid.NewGuid(), "Timing test"));
        var reversed = await ReadAsync<PaymentRow>(reversedResponse);
        var beforeReversal = new FinanceService(db, new FixedClock(new DateTimeOffset(reversed.Reversal!.PostedAtUtc.AddTicks(-1), TimeSpan.Zero)));
        Assert.Equal("backdated_settlement", (await Assert.ThrowsAsync<ErpException>(() => beforeReversal.PayAsync(input with { RequestId = Guid.NewGuid() }, actor, default))).Code);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [SqlServerFact]
    public async Task Reversal_clock_before_original_rolls_back_without_effect()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(FinanceDirection.Receivable);
        var payment = await PayAsync(client, new(Guid.NewGuid(), s.Partner, FinanceDirection.Receivable,
            false, s.Method, 100, null, [new(s.Debits[0], 100)]));
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = new FinanceService(db, new FixedClock(new DateTimeOffset(payment.PostedAtUtc.AddTicks(-1), TimeSpan.Zero)));
        var error = await Assert.ThrowsAsync<ErpException>(() => service.ReversePaymentAsync(payment.Id,
            new(Guid.NewGuid(), "Clock test"), new(Guid.NewGuid(), "clock-test"), default));
        Assert.Equal("reversal_before_payment", error.Code);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.False(await db.Set<PaymentReversal>().AnyAsync(x => x.PaymentId == payment.Id));
    }

    [SqlServerFact]
    public async Task Accountant_can_post_but_cannot_reverse_without_separate_grant()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(FinanceDirection.Receivable);
        var payment = await PayAsync(admin, new(Guid.NewGuid(), s.Partner, FinanceDirection.Receivable,
            false, s.Method, 100, null, [new(s.Debits[0], 100)]));
        var email = $"reverse-accountant-{Guid.NewGuid():N}@minierp.example";
        var password = Guid.NewGuid().ToString("N") + "Aa7!";
        using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Payment operator", password, "Accountant"));
        await ReadAsync<UserAccount>(created);
        using var accountant = await fixture.AuthenticatedClientAsync(email, password);
        using var denied = await accountant.PostAsJsonAsync($"/api/v1/finance/payments/{payment.Id}/reverse",
            new PaymentReversalInput(Guid.NewGuid(), "Requires separate grant"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ErpDbContext>().Set<PaymentReversal>().AnyAsync(x => x.PaymentId == payment.Id));
    }

    [SqlServerFact]
    public async Task Multi_order_reversal_preserves_original_and_reopens_both_directions()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        foreach (var direction in Enum.GetValues<FinanceDirection>())
        {
            var s = await SeedAsync(direction);
            var input = new PaymentInput(Guid.NewGuid(), s.Partner, direction, false, s.Method, 150, "immutable",
                [new(s.Debits[0], 100), new(s.Debits[1], 50)]);
            var payment = await PayAsync(client, input);
            var command = new PaymentReversalInput(Guid.NewGuid(), "Wrong bank allocation");
            var path = $"/api/v1/finance/payments/{payment.Id}/reverse";
            var results = await Task.WhenAll(client.PostAsJsonAsync(path, command), client.PostAsJsonAsync(path, command));
            try
            {
                var first = await ReadAsync<PaymentRow>(results[0]);
                var second = await ReadAsync<PaymentRow>(results[1]);
                Assert.NotNull(first.Reversal);
                Assert.Equal(first.Reversal, second.Reversal);
                Assert.True(first.Reversal.PostedAtUtc >= payment.PostedAtUtc);
                Assert.Equal(payment, first with { Reversal = null, Allocations = payment.Allocations });
            }
            finally { foreach (var result in results) result.Dispose(); }
            using var changed = await client.PostAsJsonAsync(path, command with { Reason = "Different" });
            Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
            var replay = await PayAsync(client, input);
            Assert.NotNull(replay.Reversal);
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var original = await db.Set<FinancePayment>().AsNoTracking().Include(x => x.Allocations).SingleAsync(x => x.Id == payment.Id);
            Assert.Equal(payment.Amount, original.Amount);
            Assert.Equal(payment.PostedAtUtc, original.PostedAtUtc);
            Assert.Equal(payment.Reference, original.Reference);
            Assert.Equal(input.Allocations.OrderBy(x => x.InvoiceId), original.Allocations.OrderBy(x => x.InvoiceId).Select(x => new AllocationInput(x.InvoiceId, x.Amount)));
            Assert.Single(await db.Set<PaymentReversal>().Where(x => x.PaymentId == payment.Id).ToArrayAsync());
            var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Partner}"))!;
            Assert.All(invoices.Items.Where(x => !x.IsCredit), x => Assert.Equal(0, x.Settled));
        }
    }

    [SqlServerFact]
    public async Task Reversal_and_refund_compete_for_pooled_order_cash_without_overrefund()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(FinanceDirection.Receivable);
        var paid = await PayAsync(client, new(Guid.NewGuid(), s.Partner, FinanceDirection.Receivable,
            false, s.Method, 100, null, [new(s.Debits[0], 100)]));
        var credit = await AddCreditAsync(s.Partner, FinanceDirection.Receivable, s.Debits[0]);
        var refund = new PaymentInput(Guid.NewGuid(), s.Partner, FinanceDirection.Receivable,
            true, s.Method, 40, null, [new(credit, 40)]);
        var results = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/finance/payments/{paid.Id}/reverse", new PaymentReversalInput(Guid.NewGuid(), "Race correction")),
            client.PostAsJsonAsync("/api/v1/finance/payments", refund));
        try
        {
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var result in results) result.Dispose(); }
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var payments = await db.Set<FinancePayment>().AsNoTracking().Where(x => x.PartnerId == s.Partner).ToArrayAsync();
        var paymentIds = payments.Select(x => x.Id).ToArray();
        var reversed = await db.Set<PaymentReversal>().Where(x => paymentIds.Contains(x.PaymentId)).Select(x => x.PaymentId).ToArrayAsync();
        Assert.True(payments.Where(x => !reversed.Contains(x.Id)).Sum(x => x.IsRefund ? -x.Amount : x.Amount) >= 0);
    }

    [SqlServerFact]
    public async Task Refund_dependency_rolls_back_and_same_scope_can_reverse_refund_then_original()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        foreach (var direction in Enum.GetValues<FinanceDirection>())
        {
            var s = await SeedAsync(direction);
            var paid = await PayAsync(client, new(Guid.NewGuid(), s.Partner, direction, false, s.Method, 100, null, [new(s.Debits[0], 100)]));
            var credit = await AddCreditAsync(s.Partner, direction, s.Debits[0]);
            var refund = await PayAsync(client, new(Guid.NewGuid(), s.Partner, direction, true, s.Method, 40, null, [new(credit, 40)]));
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<FinanceService>();
            var actor = new RequestActor(Guid.NewGuid(), "reversal-rollback");
            var command = new PaymentReversalInput(Guid.NewGuid(), "Correction");
            var error = await Assert.ThrowsAsync<ErpException>(() => service.ReversePaymentAsync(paid.Id, command, actor, default));
            Assert.Equal("dependent_refund", error.Code);
            Assert.False(db.ChangeTracker.HasChanges());
            Assert.False(await db.Set<PaymentReversal>().AnyAsync(x => x.PaymentId == paid.Id));
            Assert.NotNull((await service.ReversePaymentAsync(refund.Id, new(Guid.NewGuid(), "Refund correction"), actor, default)).Reversal);
            Assert.NotNull((await service.ReversePaymentAsync(paid.Id, command, actor, default)).Reversal);
            var duplicate = await service.ReversePaymentAsync(paid.Id, command, actor, default);
            Assert.NotNull(duplicate.Reversal);
            await Assert.ThrowsAsync<ErpException>(() => service.ReversePaymentAsync(refund.Id, command, actor, default));
        }
    }

    private async Task<(Guid Partner, Guid Method, long[] Debits)> SeedAsync(FinanceDirection direction)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var code = Guid.NewGuid().ToString("N")[..12];
        var partner = new BusinessPartner { Kind = direction == FinanceDirection.Receivable ? PartnerKind.Customer : PartnerKind.Supplier, Code = code, Name = code };
        var warehouse = new CatalogLookup { Kind = LookupKind.Warehouse, Code = code, Name = code };
        var method = new CatalogLookup { Kind = LookupKind.PaymentMethod, Code = code, Name = code };
        db.AddRange(partner, warehouse, method);
        await db.SaveChangesAsync();
        var ids = new List<long>();
        for (var index = 0; index < 2; index++)
        {
            var stamp = DateTime.UtcNow;
            var order = new TradeOrder { RequestId = Guid.NewGuid(), Kind = direction == FinanceDirection.Receivable ? OrderKind.Sales : OrderKind.Purchase,
                PartnerId = partner.Id, WarehouseId = warehouse.Id, CreatedAtUtc = stamp, UpdatedAtUtc = stamp };
            var stock = new StockDocument { RequestId = Guid.NewGuid(), Kind = StockKind.Receipt, WarehouseId = warehouse.Id, CreatedAtUtc = stamp };
            stock.Post(stamp);
            db.AddRange(order, stock);
            await db.SaveChangesAsync();
            var invoice = new FinanceInvoice { StockDocumentId = stock.Id, OrderId = order.Id, OrderNumber = "COR-" + order.Id,
                PartnerId = partner.Id, Direction = direction, Amount = 100, IssuedAtUtc = stamp, DueAtUtc = stamp };
            db.Add(invoice);
            await db.SaveChangesAsync();
            ids.Add(invoice.Id);
        }
        return (partner.Id, method.Id, ids.ToArray());
    }

    private async Task<long> AddCreditAsync(Guid partner, FinanceDirection direction, long debitId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var debit = await db.Set<FinanceInvoice>().SingleAsync(x => x.Id == debitId);
        var order = await db.Orders.SingleAsync(x => x.Id == debit.OrderId);
        var stamp = DateTime.UtcNow;
        var stock = new StockDocument { RequestId = Guid.NewGuid(), Kind = StockKind.Receipt, WarehouseId = order.WarehouseId, CreatedAtUtc = stamp };
        stock.Post(stamp); db.Add(stock); await db.SaveChangesAsync();
        var credit = new FinanceInvoice { StockDocumentId = stock.Id, OrderId = order.Id, OrderNumber = debit.OrderNumber,
            PartnerId = partner, Direction = direction, IsCredit = true, Amount = 100, IssuedAtUtc = stamp, DueAtUtc = stamp };
        db.Add(credit); await db.SaveChangesAsync();
        return credit.Id;
    }

    private static async Task<PaymentRow> PayAsync(HttpClient client, PaymentInput input)
    {
        using var result = await client.PostAsJsonAsync("/api/v1/finance/payments", input);
        return await ReadAsync<PaymentRow>(result);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private sealed class FixedClock(DateTimeOffset stamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => stamp;
    }
}
