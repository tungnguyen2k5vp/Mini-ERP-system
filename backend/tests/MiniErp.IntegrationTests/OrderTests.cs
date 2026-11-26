using System.Net;
using System.Net.Http.Json;
using MiniErp.Application;
using MiniErp.Application.Catalog;
using MiniErp.Application.Inventory;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;
namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class OrderTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Sales_returns_use_only_outstanding_shipment_cost_and_reconcile_invoice_cents()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        async Task Receive(decimal price)
        {
            var purchase = await ApproveAsync(client, await CreateAsync(client,
                new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 2, price)])));
            await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        }
        await Receive(10);
        var sale = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 2, 40)])));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1)]));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        await Receive(30); // Two units at 10 plus two at 30: the next shipment costs 20.
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1)]));
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        var balance = await BalanceAsync(client, s.Product);
        Assert.Equal(4, balance.OnHand); Assert.Equal(20, balance.AverageCost); Assert.Equal(0, balance.Reserved);
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(4, invoices.Count);
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        var lastReturn = sale.Fulfillments.Where(x => x.IsReturn).MaxBy(x => x.Id)!;
        Assert.Equal(20, Assert.Single(invoices, x => x.StockDocumentId == lastReturn.StockDocumentId).CostAmount);
        var fractional = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 1, 40)])));
        await FulfillAsync(client, fractional.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1)]));
        foreach (var quantity in new[] { 0.3333m, 0.3333m, 0.3334m })
            fractional = await FulfillAsync(client, fractional.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, quantity)]));
        var finalFraction = fractional.Fulfillments.Where(x => x.IsReturn).MaxBy(x => x.Id)!;
        invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(6.66m, Assert.Single(invoices, x => x.StockDocumentId == finalFraction.StockDocumentId).CostAmount);
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        using var reversed = await client.PostAsync($"/api/v1/orders/{fractional.Order.Id}/fulfillments/{finalFraction.Id}/reverse", null);
        await ReadAsync<OrderDetail>(reversed);
        await FulfillAsync(client, fractional.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 0.3334m)]));
        invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        Assert.Equal(4, (await BalanceAsync(client, s.Product)).OnHand);
    }
    [SqlServerFact]
    public async Task Fractional_partial_returns_preserve_last_cent_and_block_reversing_consumed_fulfillments()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var order = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 1, 0.01m)])));
        foreach (var quantity in new[] { 0.3333m, 0.3333m, 0.3334m }) order = await FulfillAsync(client, order.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, quantity)]));
        Assert.Equal(0.01m, order.Fulfillments.Sum(x => x.Amount));
        foreach (var quantity in new[] { 0.3333m, 0.3333m, 0.3334m }) order = await FulfillAsync(client, order.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, quantity)]));
        Assert.Equal(0.01m, order.Fulfillments.Where(x => x.IsReturn).Sum(x => x.Amount));
        var firstShipment = order.Fulfillments.First(x => !x.IsReturn);
        using var blocked = await client.PostAsync($"/api/v1/orders/{order.Order.Id}/fulfillments/{firstShipment.Id}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var after = await client.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{order.Order.Id}");
        Assert.Equal(1, after!.Lines[0].FulfilledQuantity); Assert.Equal(1, after.Lines[0].ReturnedQuantity);
        Assert.Equal(0, (await BalanceAsync(client, s.Product)).OnHand);
    }
    [SqlServerFact]
    public async Task Purchase_partial_receipts_retry_return_reversal_and_sales_reservation_reconcile()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var setup = await SetupAsync(client);
        var input = new OrderInput(Guid.NewGuid(), OrderKind.Purchase, setup.Supplier, setup.Warehouse, null, [new(setup.Product, 10, 100)]);
        var purchase = await CreateAsync(client, input);
        Assert.Equal(purchase.Order.Id, (await CreateAsync(client, input)).Order.Id);
        purchase = await ApproveAsync(client, purchase);
        var partial = new FulfillmentInput(Guid.NewGuid(), false, [new(setup.Product, 4)]);
        purchase = await FulfillAsync(client, purchase.Order.Id, partial);
        Assert.Equal(OrderStatus.Partial, purchase.Order.Status);
        Assert.Equal(4, (await FulfillAsync(client, purchase.Order.Id, partial)).Lines[0].FulfilledQuantity);
        using var over = await client.PostAsJsonAsync($"/api/v1/orders/{purchase.Order.Id}/fulfill", partial with { RequestId = Guid.NewGuid(), Lines = [new(setup.Product, 7)] });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        purchase = await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(setup.Product, 6)]));
        Assert.Equal(OrderStatus.Fulfilled, purchase.Order.Status);
        purchase = await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), true, [new(setup.Product, 2)]));
        Assert.Equal(8, (await BalanceAsync(client, setup.Product)).OnHand);
        var returned = Assert.Single(purchase.Fulfillments, x => x.IsReturn);
        using var reverse = await client.PostAsync($"/api/v1/orders/{purchase.Order.Id}/fulfillments/{returned.Id}/reverse", null);
        purchase = await ReadAsync<OrderDetail>(reverse);
        Assert.Equal(0, purchase.Lines[0].ReturnedQuantity);
        Assert.Equal(10, (await BalanceAsync(client, setup.Product)).OnHand);
        using var linkedReverse = await client.PostAsync($"/api/v1/inventory/documents/{returned.StockDocumentId}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, linkedReverse.StatusCode);

        var sale = await CreateAsync(client, input with { RequestId = Guid.NewGuid(), Kind = OrderKind.Sales, PartnerId = setup.Customer, Lines = [new(setup.Product, 8, 200)] });
        sale = await ApproveAsync(client, sale);
        Assert.Equal(8, (await BalanceAsync(client, setup.Product)).Reserved);
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(setup.Product, 3)]));
        var balance = await BalanceAsync(client, setup.Product);
        Assert.Equal(7, balance.OnHand); Assert.Equal(5, balance.Reserved); Assert.Equal(2, balance.Available);
        sale = await TransitionAsync(client, sale, "cancel");
        Assert.Equal(0, (await BalanceAsync(client, setup.Product)).Reserved);
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(setup.Product, 1)]));
        Assert.Equal(8, (await BalanceAsync(client, setup.Product)).OnHand);
        Assert.Equal(OrderStatus.Cancelled, sale.Order.Status);
        var history = await client.GetFromJsonAsync<Page<OrderRow>>($"/api/v1/orders?kind=2&partnerId={setup.Customer}");
        Assert.Equal(sale.Order.Id, Assert.Single(history!.Items).Id);
    }
    [SqlServerFact]
    public async Task Competing_reservations_and_stale_transition_cannot_oversell()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 10, 100)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 10)]));
        var a = await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 8, 200)]));
        var b = await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 8, 200)]));
        var stale = a;
        a = await TransitionAsync(client, a, "submit"); b = await TransitionAsync(client, b, "submit");
        using var staleResponse = await client.PostAsJsonAsync($"/api/v1/orders/{a.Order.Id}/approve", new OrderVersion(stale.Order.Version));
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/orders/{a.Order.Id}/approve", new OrderVersion(a.Order.Version)),
            client.PostAsJsonAsync($"/api/v1/orders/{b.Order.Id}/approve", new OrderVersion(b.Order.Version)));
        try { Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(8, (await BalanceAsync(client, s.Product)).Reserved);
    }
    [SqlServerFact]
    public async Task Approval_failure_rolls_back_all_line_reservations_and_unauthorized_requests_are_rejected()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        using var anonymous = fixture.NewClient();
        using var denied = await anonymous.GetAsync("/api/v1/orders?kind=1");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var s = await SetupAsync(client); var other = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 10, 100)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 10)]));
        var sale = await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 5, 200), new(other.Product, 1, 200)]));
        sale = await TransitionAsync(client, sale, "submit");
        using var failed = await client.PostAsJsonAsync($"/api/v1/orders/{sale.Order.Id}/approve", new OrderVersion(sale.Order.Version));
        Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
        Assert.Equal(0, (await BalanceAsync(client, s.Product)).Reserved);
        var unchanged = await client.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{sale.Order.Id}");
        Assert.Equal(OrderStatus.PendingApproval, unchanged!.Order.Status);
    }
    private static async Task<(Guid Warehouse, Guid Product, Guid Supplier, Guid Customer)> SetupAsync(HttpClient client)
    {
        var code = Guid.NewGuid().ToString("N")[..12];
        async Task<Guid> Lookup(string path, string suffix)
        {
            using var r = await client.PostAsJsonAsync("/api/v1/" + path, new LookupInput(code + suffix, "Test " + code, true, null));
            return (await ReadAsync<LookupResponse>(r)).Id;
        }
        var warehouse = await Lookup("warehouses", "W"); var category = await Lookup("categories", "C"); var unit = await Lookup("units", "U");
        using var p = await client.PostAsJsonAsync("/api/v1/products", new ProductInput(code, "Test " + code, category, unit, 100, 200, 0, null, null, true, null));
        var product = (await ReadAsync<ProductResponse>(p)).Id;
        async Task<Guid> Partner(string path)
        {
            using var r = await client.PostAsJsonAsync("/api/v1/" + path, new PartnerInput(code, "Test " + code, null, null, null, null, 0, 30, true, null));
            return (await ReadAsync<PartnerResponse>(r)).Id;
        }
        return (warehouse, product, await Partner("suppliers"), await Partner("customers"));
    }
    private static async Task<OrderDetail> CreateAsync(HttpClient client, OrderInput input)
    { using var r = await client.PostAsJsonAsync("/api/v1/orders", input); return await ReadAsync<OrderDetail>(r); }
    private static async Task<OrderDetail> TransitionAsync(HttpClient client, OrderDetail order, string action)
    { using var r = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/{action}", new OrderVersion(order.Order.Version)); return await ReadAsync<OrderDetail>(r); }
    private static async Task<OrderDetail> ApproveAsync(HttpClient client, OrderDetail order) => await TransitionAsync(client, await TransitionAsync(client, order, "submit"), "approve");
    private static async Task<OrderDetail> FulfillAsync(HttpClient client, int id, FulfillmentInput input)
    { using var r = await client.PostAsJsonAsync($"/api/v1/orders/{id}/fulfill", input); return await ReadAsync<OrderDetail>(r); }
    private static async Task<StockBalanceRow> BalanceAsync(HttpClient client, Guid product) =>
        Assert.Single((await client.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?productId={product}"))!.Items);
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); return (await response.Content.ReadFromJsonAsync<T>())!; }
}
