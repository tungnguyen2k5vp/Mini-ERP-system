using System.Net;
using System.Net.Http.Json;
using MiniErp.Application;
using MiniErp.Application.Catalog;
using MiniErp.Application.Inventory;
using MiniErp.Domain.Inventory;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class InventoryTests(ErpFixture fixture)
{
    private const string Root = "/api/v1/inventory";

    [SqlServerFact]
    public async Task Receipts_issue_transfer_retries_and_reversal_preserve_quantities_and_cost()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (source, dest, product) = await SetUpAsync(client);
        var opening = new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, source, null, null, [new(product, 100, 100)]);
        int openingId = (await CreateAsync(client, opening)).Document.Id;
        Assert.Equal(openingId, (await CreateAsync(client, opening)).Document.Id);
        using var reusedKey = await client.PostAsJsonAsync(Root + "/documents", opening with { Note = "different" });
        Assert.Equal(HttpStatusCode.Conflict, reusedKey.StatusCode);
        await PostAsync(client, openingId);
        await PostAsync(client, (await CreateAsync(client, opening with
        {
            RequestId = Guid.NewGuid(), Lines = [new(product, 50, 160)]
        })).Document.Id);
        var issue = await DraftAsync(client, StockKind.Issue, source, product, 20);
        await PostAsync(client, issue);
        await PostAsync(client, issue);

        var transfer = await DraftAsync(client, StockKind.Transfer, source, product, 10, destination: dest);
        await PostAsync(client, transfer);
        var balances = await BalancesAsync(client, product);
        Assert.Equal(120, Assert.Single(balances, x => x.WarehouseId == source).OnHand);
        Assert.Equal(10, Assert.Single(balances, x => x.WarehouseId == dest).OnHand);
        Assert.Equal(130, balances.Sum(x => x.OnHand));
        Assert.All(balances, row =>
        {
            Assert.Equal(120, row.AverageCost);
            Assert.Equal(0, row.Reserved);
        });

        using var blocked = await client.PostAsync($"{Root}/documents/{openingId}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using var reverse = await client.PostAsync($"{Root}/documents/{transfer}/reverse", null);
        var reversal = await ReadAsync<StockDocumentResponse>(reverse);
        Assert.Equal(transfer, reversal.Document.ReversesId);
        using var repeat = await client.PostAsync($"{Root}/documents/{transfer}/reverse", null);
        Assert.Equal(reversal.Document.Id, (await ReadAsync<StockDocumentResponse>(repeat)).Document.Id);
        var afterReversal = await BalancesAsync(client, product);
        Assert.Equal(130, Assert.Single(afterReversal, x => x.WarehouseId == source).OnHand);
        Assert.Equal(0, Assert.Single(afterReversal, x => x.WarehouseId == dest).OnHand);
        var history = await client.GetFromJsonAsync<Page<StockMovementRow>>($"{Root}/history?productId={product}");
        Assert.Equal(7, history!.Total);
        Assert.Equal(120, Assert.Single(history.Items, x => x.DocumentId == issue).UnitCost);
    }

    [SqlServerFact]
    public async Task Competing_issues_only_allow_one_to_consume_the_available_stock_and_count_detects_change()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (source, _, product) = await SetUpAsync(client);
        await PostAsync(client, await DraftAsync(client, StockKind.Receipt, source, product, 10, unitCost: 50));
        int count = await DraftAsync(client, StockKind.Count, source, product, 10, note: "Đếm trước khi xuất");
        int a = await DraftAsync(client, StockKind.Issue, source, product, 8);
        int b = await DraftAsync(client, StockKind.Issue, source, product, 8);
        var issued = await Task.WhenAll(client.PostAsync($"{Root}/documents/{a}/post", null),
            client.PostAsync($"{Root}/documents/{b}/post", null));
        try
        {
            Assert.Single(issued, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(issued, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in issued) response.Dispose();
        }
        using var staleCount = await client.PostAsync($"{Root}/documents/{count}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, staleCount.StatusCode);
        Assert.Equal(2, Assert.Single(await BalancesAsync(client, product)).Available);
    }

    [SqlServerFact]
    public async Task Multi_line_post_rolls_back_if_one_product_has_no_stock()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (source, _, first) = await SetUpAsync(client);
        var (_, _, second) = await SetUpAsync(client);
        var products = new[] { first, second }.Order().ToArray();
        await PostAsync(client, await DraftAsync(client, StockKind.Receipt, source, products[0], 20, unitCost: 30));

        var issue = await CreateAsync(client, new StockDocumentInput(Guid.NewGuid(), StockKind.Issue, source, null, null,
            [new(products[0], 10, null), new(products[1], 10, null)]));
        using var refusal = await client.PostAsync($"{Root}/documents/{issue.Document.Id}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, refusal.StatusCode);
        var detail = await client.GetFromJsonAsync<StockDocumentResponse>($"{Root}/documents/{issue.Document.Id}");
        Assert.Equal(StockStatus.Draft, detail!.Document.Status);
        Assert.Equal(20, Assert.Single(await BalancesAsync(client, products[0])).OnHand);
        Assert.Empty(await BalancesAsync(client, products[1]));
        var history = await client.GetFromJsonAsync<Page<StockMovementRow>>($"{Root}/history?productId={products[0]}");
        Assert.Single(history!.Items);
    }

    [SqlServerFact]
    public async Task Adjustment_count_and_draft_cancellation_apply_only_posted_changes()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (source, _, product) = await SetUpAsync(client);
        await PostAsync(client, await DraftAsync(client, StockKind.Receipt, source, product, 10, unitCost: 50));
        await PostAsync(client, await DraftAsync(client, StockKind.Adjustment, source, product, -3, note: "Điều chỉnh giảm"));
        await PostAsync(client, await DraftAsync(client, StockKind.Adjustment, source, product, 2, unitCost: 101, note: "Điều chỉnh tăng"));

        var count = await DraftAsync(client, StockKind.Count, source, product, 6, note: "Đếm thực tế");
        Assert.Equal(9, Assert.Single(await BalancesAsync(client, product)).OnHand);
        await PostAsync(client, count);
        var balance = Assert.Single(await BalancesAsync(client, product));
        Assert.Equal(6, balance.OnHand);
        Assert.Equal(61.3333m, balance.AverageCost);

        var issue = await DraftAsync(client, StockKind.Issue, source, product, 4);
        using var cancel = await client.PostAsync($"{Root}/documents/{issue}/cancel", null);
        Assert.Equal(StockStatus.Cancelled, (await ReadAsync<StockDocumentResponse>(cancel)).Document.Status);
        using var post = await client.PostAsync($"{Root}/documents/{issue}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
        Assert.Equal(6, Assert.Single(await BalancesAsync(client, product)).OnHand);
    }

    private static async Task<(Guid Source, Guid Destination, Guid Product)> SetUpAsync(HttpClient client)
    {
        var code = Guid.NewGuid().ToString("N")[..10];
        async Task<Guid> Lookup(string resource, string suffix)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/" + resource, new LookupInput(code + suffix,
                "Kiểm thử " + code + suffix, true, null));
            return (await ReadAsync<LookupResponse>(response)).Id;
        }

        var source = await Lookup("warehouses", "A");
        var destination = await Lookup("warehouses", "B");
        var category = await Lookup("categories", "C");
        var unit = await Lookup("units", "U");
        using var product = await client.PostAsJsonAsync("/api/v1/products",
            new ProductInput(code, "Sản phẩm kho " + code, category, unit, 100, 200, 5, null, null, true, null));
        return (source, destination, (await ReadAsync<ProductResponse>(product)).Id);
    }

    private static async Task<int> DraftAsync(HttpClient client, StockKind kind, Guid warehouse, Guid product, decimal amount,
        decimal? unitCost = null, Guid? destination = null, string? note = null) =>
        (await CreateAsync(client, new StockDocumentInput(Guid.NewGuid(), kind, warehouse, destination,
            note, [new(product, amount, unitCost)]))).Document.Id;

    private static async Task<StockDocumentResponse> CreateAsync(HttpClient client, StockDocumentInput input)
    {
        using var result = await client.PostAsJsonAsync(Root + "/documents", input);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return await ReadAsync<StockDocumentResponse>(result);
    }

    private static async Task PostAsync(HttpClient client, int id)
    {
        using var response = await client.PostAsync($"{Root}/documents/{id}/post", null);
        Assert.Equal(StockStatus.Posted, (await ReadAsync<StockDocumentResponse>(response)).Document.Status);
    }

    private static async Task<IReadOnlyList<StockBalanceRow>> BalancesAsync(HttpClient client, Guid product) =>
        (await client.GetFromJsonAsync<Page<StockBalanceRow>>($"{Root}/balances?productId={product}"))!.Items;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"Unexpected HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
