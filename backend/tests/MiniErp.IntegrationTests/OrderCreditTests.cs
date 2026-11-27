using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Orders;
using Microsoft.Data.SqlClient;

namespace MiniErp.IntegrationTests;

public sealed partial class OrderTests
{
    [SqlServerFact]
    public async Task Concurrent_credit_limit_decrease_blocks_approval_then_approval_rechecks_committed_limit()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        await SetCreditLimitAsync(s.Customer, 100);
        var sale = await TransitionAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Sales,
            s.Customer, s.Warehouse, null, [new(s.Product, 1, 80)])), "submit");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        await using var limitScope = fixture.Services.CreateAsyncScope();
        var limitDb = limitScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var change = await limitDb.Database.BeginTransactionAsync(ct);
        // Completion of this update synchronizes the test: the decreased limit is staged under an X lock.
        await limitDb.Partners.Where(x => x.Id == s.Customer).ExecuteUpdateAsync(set => set.SetProperty(x => x.CreditLimit, 50m), ct);
        await using var approvalScope = fixture.Services.CreateAsyncScope();
        var approvalDb = approvalScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = approvalScope.ServiceProvider.GetRequiredService<OrderService>();
        await approvalDb.Database.OpenConnectionAsync(ct);
        try
        {
            await approvalDb.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 500;", ct);
            var actor = new RequestActor(null, "concurrent-limit-decrease");
            var error = await Record.ExceptionAsync(() => Task.Run(() => service.TransitionAsync(sale.Order.Id, "approve",
                new(sale.Order.Version), actor, ct, canSelfApprove: true, canApproveAboveLimit: true), ct));
            Assert.NotNull(error);
            while (error is not SqlException && error.InnerException is not null) error = error.InnerException;
            Assert.Equal(1222, Assert.IsType<SqlException>(error).Number);
            await change.CommitAsync(ct);
            await change.DisposeAsync();
            await approvalDb.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT -1;", ct);
            var rejected = await Assert.ThrowsAsync<MiniErp.Domain.ErpException>(() => service.TransitionAsync(sale.Order.Id, "approve",
                new(sale.Order.Version), actor, ct, canSelfApprove: true, canApproveAboveLimit: true));
            Assert.Equal("credit_limit_override_required", rejected.Code);
            var persisted = await service.DetailAsync(sale.Order.Id, ct);
            Assert.Equal(OrderStatus.PendingApproval, persisted.Order.Status);
            Assert.Equal(sale.Order.Version, persisted.Order.Version);
        }
        finally
        {
            await approvalDb.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT -1;", CancellationToken.None);
            await approvalDb.Database.CloseConnectionAsync();
        }
    }

    [SqlServerFact]
    public async Task Credit_serializes_same_customer_approvals_across_independent_skus_and_audits_override()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var a = await SetupAsync(admin);
        var b = await SetupAsync(admin);
        async Task<OrderDetail> Sale(Guid product) => await TransitionAsync(admin, await CreateAsync(admin,
            new(Guid.NewGuid(), OrderKind.Sales, a.Customer, a.Warehouse, null, [new(product, 1, 80)])), "submit");
        var purchase = await ApproveAsync(admin, await CreateAsync(admin, new(Guid.NewGuid(), OrderKind.Purchase,
            a.Supplier, a.Warehouse, null, [new(a.Product, 2, 10), new(b.Product, 2, 10)])));
        await FulfillAsync(admin, purchase.Order.Id, new(Guid.NewGuid(), false, [new(a.Product, 2), new(b.Product, 2)]));
        var first = await Sale(a.Product);
        var second = await Sale(b.Product);
        await SetCreditLimitAsync(a.Customer, 100);
        var responses = await Task.WhenAll(admin.PostAsJsonAsync($"/api/v1/orders/{first.Order.Id}/approve", new OrderVersion(first.Order.Version)),
            admin.PostAsJsonAsync($"/api/v1/orders/{second.Order.Id}/approve", new OrderVersion(second.Order.Version)));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        first = (await admin.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{first.Order.Id}"))!;
        second = (await admin.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{second.Order.Id}"))!;
        var pending = first.Order.Status == OrderStatus.PendingApproval ? first : second;
        var exposure = (await admin.GetFromJsonAsync<CreditExposure>($"/api/v1/orders/{pending.Order.Id}/credit-exposure"))!;
        Assert.Equal(80, exposure.Used); Assert.Equal(160, exposure.Projected); Assert.True(exposure.RequiresOverride);
        var email = $"credit-peer-{Guid.NewGuid():N}@minierp.example";
        var password = Guid.NewGuid().ToString("N") + "Aa7!";
        using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Credit peer", password, "Manager"));
        await ReadAsync<UserAccount>(created);
        using var peer = await fixture.AuthenticatedClientAsync(email, password);
        Assert.True((await peer.GetFromJsonAsync<CreditExposure>($"/api/v1/orders/{pending.Order.Id}/credit-exposure"))!.RequiresOverride);
        using var denied = await peer.PostAsJsonAsync($"/api/v1/orders/{pending.Order.Id}/approve", new OrderVersion(pending.Order.Version, "Requested exception"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var command = new OrderVersion(pending.Order.Version, "Approved credit exception");
        using var approved = await admin.PostAsJsonAsync($"/api/v1/orders/{pending.Order.Id}/approve", command);
        var result = await ReadAsync<OrderDetail>(approved);
        using var retry = await admin.PostAsJsonAsync($"/api/v1/orders/{pending.Order.Id}/approve", command);
        Assert.Equal(result.Order.Version, (await ReadAsync<OrderDetail>(retry)).Order.Version);
        Assert.Single(result.History!, x => x.Action == "OverrideCreditLimit" && x.Reason == command.Reason);
        using var changed = await admin.PostAsJsonAsync($"/api/v1/orders/{pending.Order.Id}/approve", command with { Reason = "Changed" });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        var staffEmail = $"credit-staff-{Guid.NewGuid():N}@minierp.example";
        using var staffCreated = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(staffEmail, "Credit restricted", password, "Sales Staff"));
        await ReadAsync<UserAccount>(staffCreated);
        using var staff = await fixture.AuthenticatedClientAsync(staffEmail, password);
        using var hidden = await staff.GetAsync($"/api/v1/orders/{pending.Order.Id}/credit-exposure");
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        await SetCreditLimitAsync(a.Customer, 0);
        exposure = (await admin.GetFromJsonAsync<CreditExposure>($"/api/v1/orders/{pending.Order.Id}/credit-exposure"))!;
        Assert.Null(exposure.Available); Assert.False(exposure.RequiresOverride); Assert.Equal(160, exposure.Used);
    }

    [SqlServerFact]
    public async Task Credit_exact_boundary_and_partial_posting_payment_cancel_return_conserve_exposure()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        await SetCreditLimitAsync(s.Customer, 100);
        var purchase = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 4, 10)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 4)]));
        var sale = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Sales,
            s.Customer, s.Warehouse, null, [new(s.Product, 4, 25)])));
        async Task<CreditExposure> Exposure() => (await client.GetFromJsonAsync<CreditExposure>($"/api/v1/orders/{sale.Order.Id}/credit-exposure"))!;
        Assert.Equal(100, (await Exposure()).Used);
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        var partial = await Exposure();
        Assert.Equal(50, partial.Outstanding); Assert.Equal(50, partial.Unbilled); Assert.Equal(100, partial.Used);
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        var methods = (await client.GetFromJsonAsync<FinanceOptions>("/api/v1/finance/options?direction=1"))!.Methods;
        using var paymentResponse = await client.PostAsJsonAsync("/api/v1/finance/payments", new PaymentInput(Guid.NewGuid(), s.Customer,
            MiniErp.Domain.Finance.FinanceDirection.Receivable, false, methods[0].Id, 20, "Credit conservation", [new(invoices[0].Id, 20)]));
        var payment = await ReadAsync<PaymentRow>(paymentResponse);
        Assert.Equal(80, (await Exposure()).Used);
        sale = await TransitionAsync(client, sale, "cancel");
        Assert.Equal(30, (await Exposure()).Used);
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        Assert.Equal(5, (await Exposure()).Used);
        using var reverseCash = await client.PostAsJsonAsync($"/api/v1/finance/payments/{payment.Id}/reverse", new PaymentReversalInput(Guid.NewGuid(), "Restore cash debt"));
        Assert.True(reverseCash.IsSuccessStatusCode, await reverseCash.Content.ReadAsStringAsync());
        Assert.Equal(25, (await Exposure()).Used);
    }

    private async Task SetCreditLimitAsync(Guid partner, decimal limit)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Partners.Where(x => x.Id == partner).ExecuteUpdateAsync(set => set.SetProperty(x => x.CreditLimit, limit));
    }
}
