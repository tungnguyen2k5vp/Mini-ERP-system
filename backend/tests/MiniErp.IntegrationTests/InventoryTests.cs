using System.Net;
using System.Net.Http.Json;
using MiniErp.Application;
using MiniErp.Application.Auth;
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
        using var changedQuantity = await client.PostAsJsonAsync(Root + "/documents", opening with { Lines = [new(product, 101, 100)] });
        Assert.Equal(HttpStatusCode.Conflict, changedQuantity.StatusCode);
        using var changedCost = await client.PostAsJsonAsync(Root + "/documents", opening with { Lines = [new(product, 100, 101)] });
        Assert.Equal(HttpStatusCode.Conflict, changedCost.StatusCode);
        await PostAsync(client, openingId);
        await PostAsync(client, (await CreateAsync(client, opening with
        {
            RequestId = Guid.NewGuid(),
            Lines = [new(product, 50, 160)]
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
        var draft = (await client.GetFromJsonAsync<StockDocumentResponse>($"{Root}/documents/{issue}"))!;
        using var cancel = await client.PostAsJsonAsync($"{Root}/documents/{issue}/cancel", new StockCancelInput(draft.Document.Version, "Lập nhầm phiếu"));
        Assert.Equal(StockStatus.Cancelled, (await ReadAsync<StockDocumentResponse>(cancel)).Document.Status);
        using var post = await client.PostAsync($"{Root}/documents/{issue}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, post.StatusCode);
        Assert.Equal(6, Assert.Single(await BalancesAsync(client, product)).OnHand);
    }

    [SqlServerFact]
    public async Task Draft_edit_versions_replay_and_cancel_are_atomic()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (warehouse, _, product) = await SetUpAsync(client);
        var original = new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, warehouse, null, null, [new(product, 10, 37)]);
        var draft = await CreateAsync(client, original);
        var url = $"{Root}/documents/{draft.Document.Id}";
        var edit = new StockDocumentEditInput(draft.Document.Version, warehouse, null, "Revised", [new(product, 12, null)]);
        using var saved = await client.PutAsJsonAsync(url, edit);
        var revised = await ReadAsync<StockDocumentResponse>(saved);
        Assert.Equal(37, Assert.Single(revised.Lines).UnitCost);
        Assert.False(draft.Document.Version.SequenceEqual(revised.Document.Version));
        Assert.Contains(revised.History, x => x.Action == "EditStockDocument");
        Assert.Equal(draft.Document.Id, (await CreateAsync(client, original)).Document.Id);
        using var changedReplay = await client.PostAsJsonAsync(Root + "/documents", original with { Note = "Revised", Lines = [new(product, 12, 37)] });
        Assert.Equal(HttpStatusCode.Conflict, changedReplay.StatusCode);
        using var staleEdit = await client.PutAsJsonAsync(url, edit);
        Assert.Equal(HttpStatusCode.Conflict, staleEdit.StatusCode);
        using var stalePost = await client.PostAsJsonAsync(url + "/post", new StockPostInput(draft.Document.Version));
        Assert.Equal(HttpStatusCode.Conflict, stalePost.StatusCode);
        using var missingVersion = await client.PostAsync(url + "/post", null);
        Assert.Equal(HttpStatusCode.Conflict, missingVersion.StatusCode);
        var cancellation = new StockCancelInput(revised.Document.Version, "Duplicate request");
        using var cancel = await client.PostAsJsonAsync(url + "/cancel", cancellation);
        var cancelled = await ReadAsync<StockDocumentResponse>(cancel);
        using var retry = await client.PostAsJsonAsync(url + "/cancel", cancellation);
        Assert.Single((await ReadAsync<StockDocumentResponse>(retry)).History, x => x.Action == "CancelStockDocument");
        using var differentReason = await client.PostAsJsonAsync(url + "/cancel", cancellation with { Reason = "Different" });
        Assert.Equal(HttpStatusCode.Conflict, differentReason.StatusCode);
        using var terminalPost = await client.PostAsJsonAsync(url + "/post", new StockPostInput(cancelled.Document.Version));
        Assert.Equal(HttpStatusCode.Conflict, terminalPost.StatusCode);
        Assert.Empty(await BalancesAsync(client, product));
    }

    [SqlServerFact]
    public async Task Count_edit_requires_explicit_recount_and_edit_post_race_never_posts_stale_lines()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (warehouse, _, product) = await SetUpAsync(client);
        await PostAsync(client, await DraftAsync(client, StockKind.Receipt, warehouse, product, 10, unitCost: 5));
        var count = await CreateAsync(client, new StockDocumentInput(Guid.NewGuid(), StockKind.Count, warehouse, null, "Count", [new(product, 9, null)]));
        var url = $"{Root}/documents/{count.Document.Id}";
        var input = new StockDocumentEditInput(count.Document.Version, warehouse, null, "Count again", [new(product, 8, null)]);
        using var missingRecount = await client.PutAsJsonAsync(url, input);
        Assert.Equal(HttpStatusCode.Conflict, missingRecount.StatusCode);
        using var edit = await client.PutAsJsonAsync(url, input with { Recount = true });
        var refreshed = await ReadAsync<StockDocumentResponse>(edit);
        using var post = await client.PostAsJsonAsync(url + "/post", new StockPostInput(refreshed.Document.Version));
        await ReadAsync<StockDocumentResponse>(post);
        Assert.Equal(8, Assert.Single(await BalancesAsync(client, product)).OnHand);

        var receipt = await CreateAsync(client, new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, warehouse, null, null, [new(product, 2, 5)]));
        var raceUrl = $"{Root}/documents/{receipt.Document.Id}";
        var raceEdit = client.PutAsJsonAsync(raceUrl, new StockDocumentEditInput(receipt.Document.Version, warehouse, null, "Race", [new(product, 3, null)]));
        var racePost = client.PostAsJsonAsync(raceUrl + "/post", new StockPostInput(receipt.Document.Version));
        var results = await Task.WhenAll(raceEdit, racePost);
        try
        {
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var result in results) result.Dispose(); }
        var final = (await client.GetFromJsonAsync<StockDocumentResponse>(raceUrl))!;
        var balance = Assert.Single(await BalancesAsync(client, product));
        Assert.Equal(final.Document.Status == StockStatus.Posted ? 10 : 8, balance.OnHand);
    }

    [SqlServerFact]
    public async Task Concurrent_create_and_lost_post_response_replay_have_single_effect()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var (warehouse, _, product) = await SetUpAsync(client);
        var input = new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, warehouse, null, null, [new(product, 2, 10)]);
        var creates = await Task.WhenAll(client.PostAsJsonAsync(Root + "/documents", input),
            client.PostAsJsonAsync(Root + "/documents", input));
        StockDocumentResponse first;
        try
        {
            first = await ReadAsync<StockDocumentResponse>(creates[0]);
            Assert.Equal(first.Document.Id, (await ReadAsync<StockDocumentResponse>(creates[1])).Document.Id);
        }
        finally { foreach (var response in creates) response.Dispose(); }
        var url = $"{Root}/documents/{first.Document.Id}";
        using var edit = await client.PutAsJsonAsync(url, new StockDocumentEditInput(first.Document.Version,
            warehouse, null, "Edited before post", [new(product, 3, null)]));
        var draft = await ReadAsync<StockDocumentResponse>(edit);
        var postInput = new StockPostInput(draft.Document.Version);
        using var posted = await client.PostAsJsonAsync(url + "/post", postInput);
        var result = await ReadAsync<StockDocumentResponse>(posted);
        using var replay = await client.PostAsJsonAsync(url + "/post", postInput);
        var repeated = await ReadAsync<StockDocumentResponse>(replay);
        Assert.Equal(result.Document.Version, repeated.Document.Version);
        Assert.Single(repeated.History, x => x.Action == "PostStockDocument");
        Assert.Equal(3, Assert.Single(await BalancesAsync(client, product)).OnHand);
        var ledger = (await client.GetFromJsonAsync<Page<StockMovementRow>>($"{Root}/history?productId={product}"))!;
        Assert.Single(ledger.Items);
    }

    [SqlServerFact]
    public async Task Warehouse_edit_preserves_hidden_costs_and_viewer_cannot_edit()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var (warehouse, _, first) = await SetUpAsync(admin);
        var (_, _, second) = await SetUpAsync(admin);
        var draft = await CreateAsync(admin, new StockDocumentInput(Guid.NewGuid(), StockKind.Receipt, warehouse,
            null, null, [new(first, 2, 73)]));
        var url = $"{Root}/documents/{draft.Document.Id}";
        foreach (var role in new[] { "Viewer", "Warehouse Staff" })
        {
            var email = $"stock-edit-{Guid.NewGuid():N}@minierp.example";
            var password = Guid.NewGuid().ToString("N") + "Aa7!";
            using var account = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Stock edit", password, role));
            Assert.Equal(HttpStatusCode.Created, account.StatusCode);
            using var user = await fixture.AuthenticatedClientAsync(email, password);
            var redacted = (await user.GetFromJsonAsync<StockDocumentResponse>(url))!;
            Assert.Null(Assert.Single(redacted.Lines).UnitCost);
            var input = new StockDocumentEditInput(redacted.Document.Version, warehouse, null, "Keep costs",
                [new(first, 4, null), new(second, 1, null)]);
            using var edit = await user.PutAsJsonAsync(url, input);
            if (role == "Viewer") Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
            else
            {
                var result = await ReadAsync<StockDocumentResponse>(edit);
                Assert.All(result.Lines, x => Assert.Null(x.UnitCost));
                var persisted = (await admin.GetFromJsonAsync<StockDocumentResponse>(url))!;
                Assert.Equal(73, Assert.Single(persisted.Lines, x => x.ProductId == first).UnitCost);
                Assert.Equal(100, Assert.Single(persisted.Lines, x => x.ProductId == second).UnitCost);
                using var explicitCost = await user.PutAsJsonAsync(url, input with
                {
                    Version = result.Document.Version, Lines = [new(first, 4, 0)]
                });
                Assert.Equal(HttpStatusCode.Forbidden, explicitCost.StatusCode);
            }
        }
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
