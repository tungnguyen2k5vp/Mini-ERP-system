using System.Data;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Catalog;
using MiniErp.Application.Inventory;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class InventoryReadConsistencyTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Cancelled_blocked_reconciliation_rolls_back_before_reusing_the_same_connection()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var report = scope.ServiceProvider.GetRequiredService<InventoryReport>();
        await db.Database.OpenConnectionAsync(); // Keep the exact physical connection for the post-cancel assertion.
        await using var blocker = new SqlConnection(db.Database.GetConnectionString());
        await blocker.OpenAsync();
        await using var blockerTransaction = await blocker.BeginTransactionAsync();
        try
        {
            await using (var command = blocker.CreateCommand())
            {
                command.Transaction = (SqlTransaction)blockerTransaction;
                command.CommandText = "SELECT COUNT_BIG(*) FROM inventory.Movements WITH (TABLOCKX,HOLDLOCK)";
                await command.ExecuteScalarAsync();
            }
            using var cancellation = new CancellationTokenSource();
            var pending = report.ReconciliationAsync(null, null, 1, 20, cancellation.Token);
            await Task.Delay(100);
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(error is OperationCanceledException or SqlException, error?.ToString());
            Assert.Null(db.Database.CurrentTransaction);
            await using var count = db.Database.GetDbConnection().CreateCommand();
            count.CommandText = "SELECT @@TRANCOUNT";
            Assert.Equal(0, Convert.ToInt32(await count.ExecuteScalarAsync()));
        }
        finally { await blockerTransaction.RollbackAsync(); }
        var next = await report.ReconciliationAsync(null, null, 1, 20, default);
        Assert.True(next.Total >= next.Items.Count);
        Assert.Null(db.Database.CurrentTransaction);
    }

    [SqlServerFact]
    public async Task Stock_pages_and_repeated_diagnostic_pages_preserve_counts_and_leave_corruption_unmodified()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var code = Guid.NewGuid().ToString("N")[..12];
        async Task<T> Post<T>(string path, object input)
        {
            using var response = await admin.PostAsJsonAsync("/api/v1/" + path, input);
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        var warehouse = await Post<LookupResponse>("warehouses", new LookupInput(code + "W", code, true, null));
        var category = await Post<LookupResponse>("categories", new LookupInput(code + "C", code, true, null));
        var unit = await Post<LookupResponse>("units", new LookupInput(code + "U", code, true, null));
        for (var i = 0; i < 3; i++)
        {
            var product = await Post<ProductResponse>("products", new ProductInput(code + i, code, category.Id, unit.Id, 10, 20, 0, null, null, true, null));
            var doc = await Post<StockDocumentResponse>("inventory/documents", new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, warehouse.Id, null, null, [new(product.Id, 2, 10)]));
            using var posted = await admin.PostAsync($"/api/v1/inventory/documents/{doc.Document.Id}/post", null);
            Assert.True(posted.IsSuccessStatusCode, await posted.Content.ReadAsStringAsync());
        }
        var first = await admin.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?warehouseId={warehouse.Id}&pageSize=2");
        var second = await admin.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?warehouseId={warehouse.Id}&pageSize=2&page=2");
        Assert.Equal(3, first!.Total); Assert.Equal(3, second!.Total);
        Assert.Equal(2, first.Items.Count); Assert.Single(second.Items);
        Assert.Equal(3, first.Items.Concat(second.Items).Select(x => x.ProductId).Distinct().Count());

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var report = scope.ServiceProvider.GetRequiredService<InventoryReport>();
        Assert.Empty((await report.ReconciliationAsync(warehouse.Id, null, 1, 2, default)).Items);
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.Balances SET OnHand=OnHand+1 WHERE WarehouseId={warehouse.Id}");
            var page1 = await report.ReconciliationAsync(warehouse.Id, null, 1, 2, default);
            var page2 = await report.ReconciliationAsync(warehouse.Id, null, 2, 2, default);
            var repeated = await report.ReconciliationAsync(warehouse.Id, null, 1, 2, default);
            Assert.Equal(3, page1.Total); Assert.Equal(3, page2.Total);
            Assert.Equal(2, page1.Items.Count); Assert.Single(page2.Items);
            Assert.Equal(page1.Items.Select(x => x.ProductId), repeated.Items.Select(x => x.ProductId));
            Assert.All(page1.Items.Concat(page2.Items), x => { Assert.Equal(3m, x.OnHand); Assert.Equal(2m, x.LedgerQuantity); });
            Assert.Equal(9m, await db.StockBalances.Where(x => x.WarehouseId == warehouse.Id).SumAsync(x => x.OnHand));
            await transaction.RollbackAsync();
        }
        Assert.Empty((await report.ReconciliationAsync(warehouse.Id, null, 1, 2, default)).Items);
    }
}
