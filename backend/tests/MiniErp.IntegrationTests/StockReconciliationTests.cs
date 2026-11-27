using System.Data;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Inventory;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class StockReconciliationTests(ErpFixture fixture)
{
    private const string Route = "/api/v1/reports/stock/reconciliation";

    [SqlServerFact]
    public async Task Healthy_partial_returns_reversals_cancellation_and_product_filters_have_no_diagnostics()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var data = await SetupAsync(client);
        var page = await client.GetFromJsonAsync<Page<StockReconciliationRow>>($"{Route}?warehouseId={data.Warehouse}");
        Assert.Equal(0, page!.Total); Assert.Empty(page.Items);
        var filtered = await client.GetFromJsonAsync<Page<StockReconciliationRow>>($"{Route}?warehouseId={data.Warehouse}&productId={data.SecondProduct}");
        Assert.Equal(0, filtered!.Total);
        using var invalid = await client.GetAsync(Route + "?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var anonymous = fixture.NewClient();
        using var denied = await anonymous.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var email = $"stock-reconcile-{Guid.NewGuid():N}@minierp.example";
        var password = Guid.NewGuid().ToString("N") + "Aa7!";
        using var account = await client.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Reconciliation accountant", password, "Accountant"));
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        using var accountant = await fixture.AuthenticatedClientAsync(email, password);
        using var forbidden = await accountant.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [SqlServerFact]
    public async Task Rollback_only_fixtures_detect_opposing_line_drift_missing_balance_and_fulfillment_mismatch()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var data = await SetupAsync(client);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var report = scope.ServiceProvider.GetRequiredService<InventoryReport>();
            foreach (var status in new[] { OrderStatus.Rejected, OrderStatus.ReturnedForRevision })
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders.Orders SET Status={(byte)status} WHERE Id={data.CancelledOrder}");
                var impossible = await report.ReconciliationAsync(data.Warehouse, data.Product, 1, 20, CancellationToken.None);
                Assert.Equal(1, Assert.Single(impossible.Items).InconsistentOrderLines);
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders.Orders SET Status={(byte)OrderStatus.Cancelled} WHERE Id={data.CancelledOrder}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.Balances SET OnHand=OnHand+1 WHERE WarehouseId={data.Warehouse} AND ProductId={data.Product}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders.OrderLines SET ReservedQuantity=ReservedQuantity+1 WHERE OrderId={data.CancelledOrder} AND ProductId={data.Product}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders.OrderLines SET ReservedQuantity=ReservedQuantity-1 WHERE OrderId={data.SecondOpenOrder} AND ProductId={data.Product}");
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM inventory.Balances WHERE WarehouseId={data.Warehouse} AND ProductId={data.SecondProduct}");
            var first = await report.ReconciliationAsync(data.Warehouse, null, 1, 1, CancellationToken.None);
            var second = await report.ReconciliationAsync(data.Warehouse, null, 2, 1, CancellationToken.None);
            Assert.Equal(2, first.Total); Assert.Equal(2, second.Total);
            Assert.NotEqual(Assert.Single(first.Items).ProductId, Assert.Single(second.Items).ProductId);
            var all = first.Items.Concat(second.Items).ToArray();
            var drift = Assert.Single(all, x => x.ProductId == data.Product);
            Assert.Equal(1, drift.OnHand - drift.LedgerQuantity);
            Assert.Equal(drift.Reserved, drift.OrderReservations);
            Assert.Equal(drift.Reserved, drift.OpenCommitment);
            Assert.Equal(2, drift.InconsistentOrderLines);
            var missing = Assert.Single(all, x => x.ProductId == data.SecondProduct);
            Assert.False(missing.HasBalance); Assert.Equal(19, missing.LedgerQuantity); Assert.Equal(1, missing.OpenCommitment);
            var filtered = await report.ReconciliationAsync(data.Warehouse, data.Product, 1, 20, CancellationToken.None);
            Assert.Equal(1, filtered.Total); Assert.Equal(2, Assert.Single(filtered.Items).InconsistentOrderLines);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders.OrderLines SET FulfilledQuantity=FulfilledQuantity+1,ReservedQuantity=ReservedQuantity-1 WHERE OrderId={data.FirstOpenOrder} AND ProductId={data.Product}");
            var sourceDrift = await report.ReconciliationAsync(data.Warehouse, data.Product, 1, 20, CancellationToken.None);
            Assert.Equal(3, Assert.Single(sourceDrift.Items).InconsistentOrderLines);
            await transaction.RollbackAsync();
        }
        var healthy = await client.GetFromJsonAsync<Page<StockReconciliationRow>>($"{Route}?warehouseId={data.Warehouse}");
        Assert.Equal(0, healthy!.Total); Assert.Empty(healthy.Items);
    }

    private static async Task<Scenario> SetupAsync(HttpClient client)
    {
        var code = Guid.NewGuid().ToString("N")[..12];
        async Task<T> Post<T>(string path, object body)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/" + path, body);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        var warehouse = await Post<LookupResponse>("warehouses", new LookupInput(code + "W", code, true, null));
        var category = await Post<LookupResponse>("categories", new LookupInput(code + "C", code, true, null));
        var unit = await Post<LookupResponse>("units", new LookupInput(code + "U", code, true, null));
        var first = await Post<ProductResponse>("products", new ProductInput(code + "A", code, category.Id, unit.Id, 10, 20, 0, null, null, true, null));
        var second = await Post<ProductResponse>("products", new ProductInput(code + "B", code, category.Id, unit.Id, 10, 20, 0, null, null, true, null));
        var supplier = await Post<PartnerResponse>("suppliers", new PartnerInput(code, code, null, null, null, null, 0, 30, true, null));
        var customer = await Post<PartnerResponse>("customers", new PartnerInput(code, code, null, null, null, null, 0, 30, true, null));
        async Task<OrderDetail> Order(OrderKind kind, params OrderLineInput[] lines)
        {
            var order = await Post<OrderDetail>("orders", new OrderInput(Guid.NewGuid(), kind, kind == OrderKind.Purchase ? supplier.Id : customer.Id, warehouse.Id, null, lines));
            order = await Post<OrderDetail>($"orders/{order.Order.Id}/submit", new OrderVersion(order.Order.Version));
            return await Post<OrderDetail>($"orders/{order.Order.Id}/approve", new OrderVersion(order.Order.Version));
        }
        var purchase = await Order(OrderKind.Purchase, new OrderLineInput(first.Id, 20, 10), new OrderLineInput(second.Id, 20, 10));
        await Post<OrderDetail>($"orders/{purchase.Order.Id}/fulfill", new FulfillmentInput(Guid.NewGuid(), false, [new(first.Id, 20), new(second.Id, 20)]));
        var cancelled = await Order(OrderKind.Sales, new OrderLineInput(first.Id, 6, 20), new OrderLineInput(second.Id, 3, 20));
        cancelled = await Post<OrderDetail>($"orders/{cancelled.Order.Id}/fulfill", new FulfillmentInput(Guid.NewGuid(), false, [new(first.Id, 2), new(second.Id, 1)]));
        cancelled = await Post<OrderDetail>($"orders/{cancelled.Order.Id}/fulfill", new FulfillmentInput(Guid.NewGuid(), true, [new(first.Id, 1)]));
        var returned = cancelled.Fulfillments.Single(x => x.IsReturn);
        cancelled = await Post<OrderDetail>($"orders/{cancelled.Order.Id}/fulfillments/{returned.Id}/reverse", new { });
        cancelled = await Post<OrderDetail>($"orders/{cancelled.Order.Id}/cancel", new OrderVersion(cancelled.Order.Version, "Khách hủy phần chưa giao"));
        var a = await Order(OrderKind.Sales, new OrderLineInput(first.Id, 4, 20), new OrderLineInput(second.Id, 1, 20));
        var b = await Order(OrderKind.Sales, new OrderLineInput(first.Id, 4, 20));
        return new(warehouse.Id, first.Id, second.Id, a.Order.Id, b.Order.Id, cancelled.Order.Id);
    }

    private sealed record Scenario(Guid Warehouse, Guid Product, Guid SecondProduct, int FirstOpenOrder, int SecondOpenOrder, int CancelledOrder);
}
