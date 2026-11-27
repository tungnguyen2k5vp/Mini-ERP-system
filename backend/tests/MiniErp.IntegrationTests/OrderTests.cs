using System.Net;
using System.Net.Http.Json;
using MiniErp.Application;
using MiniErp.Application.Catalog;
using MiniErp.Application.Inventory;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;
using MiniErp.Application.Auth;
using MiniErp.Domain;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Orders;
using MiniErp.Infrastructure.Persistence;
namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed partial class OrderTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Detail_holds_document_lock_until_ambient_read_committed_transaction_ends()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var order = await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, "Before locked detail", [new(s.Product, 1, 10)]));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ct = deadline.Token;
        await using var readerScope = fixture.Services.CreateAsyncScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await using var ambient = await reader.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        // Returning from DetailAsync is the synchronization point: its own read scope has ended,
        // but the caller's READ COMMITTED transaction (including RCSI databases) still owns the lock.
        var detail = await readerScope.ServiceProvider.GetRequiredService<OrderService>().DetailAsync(order.Order.Id, ct);
        var edit = new OrderEditInput(detail.Order.Version, s.Supplier, s.Warehouse, "After reader release", null, [new(s.Product, 2, 11)]);
        var actor = new RequestActor(null, "detail-lock-regression");
        await using var writerScope = fixture.Services.CreateAsyncScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = writerScope.ServiceProvider.GetRequiredService<OrderService>();
        await writer.Database.OpenConnectionAsync(ct);
        try
        {
            await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT 500;", ct);
            var error = await Record.ExceptionAsync(() => Task.Run(() => service.EditAsync(order.Order.Id, edit, actor, ct), ct));
            Assert.NotNull(error);
            while (error is not SqlException && error.InnerException is not null) error = error.InnerException;
            Assert.Equal(1222, Assert.IsType<SqlException>(error).Number);
            Assert.Equal(detail.Order.Version, (await readerScope.ServiceProvider.GetRequiredService<OrderService>()
                .DetailAsync(order.Order.Id, ct)).Order.Version);
            await ambient.CommitAsync(ct);
            await ambient.DisposeAsync();
            await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT -1;", ct);
            var saved = await Task.Run(() => service.EditAsync(order.Order.Id, edit, actor, ct), ct);
            Assert.Equal(edit.Note, saved.Note);
            Assert.Equal(2, Assert.Single(saved.Lines).Quantity);
            Assert.NotEqual(detail.Order.Version, saved.Order.Version);
            Assert.Single(saved.History!, x => x.Action == "EditOrder");
        }
        finally
        {
            await writer.Database.ExecuteSqlRawAsync("SET LOCK_TIMEOUT -1;", CancellationToken.None);
            await writer.Database.CloseConnectionAsync();
        }
    }

    [SqlServerFact]
    public async Task Different_users_competing_draft_edits_persist_one_complete_winner()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var a = await SetupAsync(admin);
        var b = await SetupAsync(admin);
        async Task<HttpClient> User()
        {
            var email = $"order-editor-{Guid.NewGuid():N}@minierp.example";
            var password = Guid.NewGuid().ToString("N") + "Aa7!";
            using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Concurrent editor", password, "Purchasing Staff"));
            await ReadAsync<UserAccount>(created);
            return await fixture.AuthenticatedClientAsync(email, password);
        }
        using var first = await User();
        using var second = await User();
        var order = await CreateAsync(admin, new(Guid.NewGuid(), OrderKind.Purchase,
            a.Supplier, a.Warehouse, "Original", [new(a.Product, 1, 10)]));
        var inputs = new[]
        {
            new OrderEditInput(order.Order.Version, a.Supplier, a.Warehouse, "First editor", new(2027, 1, 2), [new(a.Product, 2, 11)]),
            new OrderEditInput(order.Order.Version, b.Supplier, b.Warehouse, "Second editor", new(2027, 2, 3), [new(b.Product, 3, 22)])
        };
        var path = $"/api/v1/orders/{order.Order.Id}";
        var responses = await Task.WhenAll(first.PutAsJsonAsync(path, inputs[0]), second.PutAsJsonAsync(path, inputs[1]));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            var winnerIndex = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.OK);
            var winner = inputs[winnerIndex];
            var response = await ReadAsync<OrderDetail>(responses[winnerIndex]);
            var persisted = (await admin.GetFromJsonAsync<OrderDetail>(path))!;
            Assert.Equal(response.Order.Version, persisted.Order.Version);
            Assert.Equal(OrderStatus.Draft, persisted.Order.Status);
            Assert.Equal(winner.PartnerId, persisted.Order.PartnerId);
            Assert.Equal(winner.WarehouseId, persisted.Order.WarehouseId);
            Assert.Equal(winner.Note, persisted.Note);
            Assert.Equal(winner.ExpectedDate, persisted.Order.ExpectedDate);
            var line = Assert.Single(persisted.Lines);
            Assert.Equal(winner.Lines[0].ProductId, line.ProductId);
            Assert.Equal(winner.Lines[0].Quantity, line.Quantity);
            Assert.Equal(winner.Lines[0].UnitPrice, line.UnitPrice);
            var edit = Assert.Single(persisted.History!, x => x.Action == "EditOrder");
            using var profileResponse = await (winnerIndex == 0 ? first : second).GetAsync("/api/v1/auth/me");
            Assert.Equal((await ReadAsync<UserProfile>(profileResponse)).Id, edit.ActorId);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [SqlServerFact]
    public async Task Http_configured_threshold_blocks_exact_boundary_and_allows_authorized_override()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(admin);
        var email = $"order-limit-{Guid.NewGuid():N}@minierp.example";
        var password = Guid.NewGuid().ToString("N") + "Aa7!";
        using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Threshold peer", password, "Manager"));
        await ReadAsync<UserAccount>(created);
        await using var configured = fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Orders:ApprovalThresholdVnd"] = "20" })));
        async Task<HttpClient> Login(string login, string secret)
        {
            var client = configured.CreateClient(new() { BaseAddress = new Uri(ErpFixture.Origin) });
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
                { Content = JsonContent.Create(new LoginRequest(login, secret)) };
            request.Headers.Add("Origin", ErpFixture.Origin);
            request.Headers.Add("X-MiniErp-Session", "1");
            using var response = await client.SendAsync(request);
            var session = await ReadAsync<MiniErp.Api.Auth.SessionResponse>(response);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            return client;
        }
        using var peer = await Login(email, password);
        using var privileged = await Login(fixture.AdminEmail, fixture.AdminPassword);
        var exact = await TransitionAsync(admin, await CreateAsync(admin, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])), "submit");
        Assert.False((await peer.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{exact.Order.Id}"))!.CanApprove);
        using var denied = await peer.PostAsJsonAsync($"/api/v1/orders/{exact.Order.Id}/approve", new OrderVersion(exact.Order.Version));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.True((await privileged.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{exact.Order.Id}"))!.CanApprove);
        Assert.Equal(OrderStatus.Approved, (await TransitionAsync(privileged, exact, "approve")).Order.Status);
        var below = await TransitionAsync(admin, await CreateAsync(admin, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 1, 19.99m)])), "submit");
        Assert.True((await peer.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{below.Order.Id}"))!.CanApprove);
        Assert.Equal(OrderStatus.Approved, (await TransitionAsync(peer, below, "approve")).Order.Status);
    }

    [SqlServerFact]
    public async Task Failed_edit_then_retry_in_same_scope_uses_persisted_status_and_version()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var order = await TransitionAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])), "submit");
        using var returned = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/return-for-revision",
            new OrderVersion(order.Order.Version, "Recheck partner"));
        order = await ReadAsync<OrderDetail>(returned);
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OrderService>();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var actor = new RequestActor(Guid.NewGuid(), "rollback-reuse");
        await Assert.ThrowsAsync<ErpException>(() => service.EditAsync(order.Order.Id,
            new(order.Order.Version, Guid.NewGuid(), s.Warehouse, "Invalid partner", null, [new(s.Product, 3, 10)]), actor, default));
        var persisted = await service.DetailAsync(order.Order.Id, default);
        Assert.Equal(OrderStatus.ReturnedForRevision, persisted.Order.Status);
        Assert.Equal(order.Order.Version, persisted.Order.Version);
        Assert.Equal(2, Assert.Single(persisted.Lines).Quantity);
        Assert.False(db.ChangeTracker.HasChanges());
        var saved = await service.EditAsync(order.Order.Id,
            new(order.Order.Version, s.Supplier, s.Warehouse, "Corrected", null, [new(s.Product, 3, 10)]), actor, default);
        var submitted = await service.TransitionAsync(order.Order.Id, "submit", new(saved.Order.Version), actor, default);
        Assert.Equal(OrderStatus.PendingApproval, submitted.Order.Status);
        Assert.Equal(3, Assert.Single(submitted.Lines).Quantity);
    }

    [SqlServerFact]
    public async Task Http_manager_cannot_approve_own_content_but_peer_can_and_staff_cannot_review()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(admin);
        async Task<HttpClient> User(string role)
        {
            var email = $"order-policy-{Guid.NewGuid():N}@minierp.example";
            var password = Guid.NewGuid().ToString("N") + "Aa7!";
            using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Order policy", password, role));
            await ReadAsync<UserAccount>(created);
            return await fixture.AuthenticatedClientAsync(email, password);
        }
        using var author = await User("Manager");
        using var peer = await User("Manager");
        using var staff = await User("Purchasing Staff");
        var order = await TransitionAsync(author, await CreateAsync(author, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])), "submit");
        Assert.False(order.CanApprove);
        Assert.False((await author.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{order.Order.Id}"))!.CanApprove);
        Assert.True((await peer.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{order.Order.Id}"))!.CanApprove);
        using var selfDenied = await author.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/approve", new OrderVersion(order.Order.Version));
        Assert.Equal(HttpStatusCode.Forbidden, selfDenied.StatusCode);
        using var staffDenied = await staff.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/return-for-revision", new OrderVersion(order.Order.Version, "Review"));
        Assert.Equal(HttpStatusCode.Forbidden, staffDenied.StatusCode);
        using var peerReturned = await peer.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/return-for-revision", new OrderVersion(order.Order.Version, "Revise"));
        order = await ReadAsync<OrderDetail>(peerReturned);
        using var edited = await peer.PutAsJsonAsync($"/api/v1/orders/{order.Order.Id}",
            new OrderEditInput(order.Order.Version, s.Supplier, s.Warehouse, "Peer edited", null, [new(s.Product, 3, 10)]));
        order = await TransitionAsync(staff, await ReadAsync<OrderDetail>(edited), "submit");
        Assert.False((await peer.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{order.Order.Id}"))!.CanApprove);
        using var editorDenied = await peer.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/approve", new OrderVersion(order.Order.Version));
        Assert.Equal(HttpStatusCode.Forbidden, editorDenied.StatusCode);
        // A separate document proves ordinary peer approval still works through HTTP.
        var other = await TransitionAsync(author, await CreateAsync(author, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])), "submit");
        other = await TransitionAsync(peer, other, "approve");
        Assert.Equal(OrderStatus.Approved, other.Order.Status);
    }

    [SqlServerFact]
    public async Task Legacy_draft_edit_and_submit_race_has_one_winner_and_keeps_original_request()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var input = new OrderInput(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, "Before migration", [new(s.Product, 2, 10)]);
        var order = await CreateAsync(client, input);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            await db.Orders.Where(x => x.Id == order.Order.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.CreatePayloadHash, (string?)null));
        }
        order = (await client.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{order.Order.Id}"))!;
        using var edited = await client.PutAsJsonAsync($"/api/v1/orders/{order.Order.Id}",
            new OrderEditInput(order.Order.Version, s.Supplier, s.Warehouse, "First edit", null, [new(s.Product, 3, 10)]));
        order = await ReadAsync<OrderDetail>(edited);
        Assert.Equal(3, Assert.Single((await CreateAsync(client, input)).Lines).Quantity);
        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/api/v1/orders/{order.Order.Id}",
                new OrderEditInput(order.Order.Version, s.Supplier, s.Warehouse, "Competing edit", null, [new(s.Product, 4, 10)])),
            client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/submit", new OrderVersion(order.Order.Version)));
        try
        {
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Equal(order.Order.Id, (await CreateAsync(client, input)).Order.Id);
    }

    [SqlServerFact]
    public async Task Revision_requires_real_edit_preserves_create_replay_and_records_reasons()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var input = new OrderInput(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, "Original",
            [new(s.Product, 2m, 10m)], DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
        var original = await CreateAsync(client, input);
        var order = await TransitionAsync(client, original, "submit");
        using var returned = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/return-for-revision",
            new OrderVersion(order.Order.Version, "Correct quantities"));
        order = await ReadAsync<OrderDetail>(returned);
        Assert.Equal(OrderStatus.ReturnedForRevision, order.Order.Status);
        using var premature = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/submit", new OrderVersion(order.Order.Version));
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        var edit = new OrderEditInput(order.Order.Version, s.Supplier, s.Warehouse, " Original ", input.ExpectedDate, [new(s.Product, 2.0000m, 10.000m)]);
        using var unchanged = await client.PutAsJsonAsync($"/api/v1/orders/{order.Order.Id}", edit);
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        using var saved = await client.PutAsJsonAsync($"/api/v1/orders/{order.Order.Id}", edit with { Lines = [new(s.Product, 3, 10)] });
        order = await ReadAsync<OrderDetail>(saved);
        Assert.Equal(OrderStatus.Draft, order.Order.Status);
        Assert.False(order.Order.IsOverdue);
        Assert.Equal(3, Assert.Single((await CreateAsync(client, input with { Note = " Original ", Lines = [new(s.Product, 2.000m, 10.0000m)] })).Lines).Quantity);
        order = await ApproveAsync(client, order);
        Assert.True(order.Order.IsOverdue);
        Assert.Contains(order.History!, x => x.Action == "return-for-revisionOrder" && x.Reason == "Correct quantities");
        using var missingReason = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/cancel", new OrderVersion(order.Order.Version));
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using var cancel = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/cancel", new OrderVersion(order.Order.Version, "No longer needed"));
        var cancelled = await ReadAsync<OrderDetail>(cancel);
        using var changedRetry = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/cancel", new OrderVersion(order.Order.Version, "Different reason"));
        Assert.Equal(HttpStatusCode.Conflict, changedRetry.StatusCode);
        Assert.False(cancelled.Order.IsOverdue);
    }

    [SqlServerFact]
    public async Task Approval_checks_authors_threshold_and_unknown_legacy_creator_under_lock()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var order = await TransitionAsync(client, await CreateAsync(client, new(Guid.NewGuid(), OrderKind.Purchase,
            s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])), "submit");
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var existing = scope.ServiceProvider.GetRequiredService<OrderService>();
        var actorId = (await db.AuditEntries.SingleAsync(x => x.Entity == "Order" && x.EntityId == order.Order.Id.ToString() && x.Action == "CreateOrder")).UserId;
        Assert.False(await existing.CanApproveAsync(order.Order.Id, actorId, false, true, default));
        Assert.True(await existing.CanApproveAsync(order.Order.Id, Guid.NewGuid(), false, true, default));
        var threshold = new OrderService(db, scope.ServiceProvider.GetRequiredService<MiniErp.Infrastructure.Inventory.StockService>(),
            scope.ServiceProvider.GetRequiredService<IFinancePosting>(), TimeProvider.System, new(20));
        Assert.False(await threshold.CanApproveAsync(order.Order.Id, Guid.NewGuid(), false, false, default));
        Assert.True(await threshold.CanApproveAsync(order.Order.Id, Guid.NewGuid(), false, true, default));
        var latestEditor = Guid.NewGuid();
        db.AuditEntries.Add(AuditEntry.Create(new(latestEditor, "editor-policy-test"), DateTime.UtcNow,
            "EditOrder", "Order", order.Order.Id.ToString()));
        await db.SaveChangesAsync();
        Assert.False(await existing.CanApproveAsync(order.Order.Id, latestEditor, false, true, default));
        Assert.True(await existing.CanApproveAsync(order.Order.Id, latestEditor, true, true, default));
        var failure = await Assert.ThrowsAsync<ErpException>(() => threshold.TransitionAsync(order.Order.Id, "approve",
            new(order.Order.Version), new(Guid.NewGuid(), "threshold-test"), default));
        Assert.Equal("approval_permission_required", failure.Code);
        db.ChangeTracker.Clear();
        await db.AuditEntries.Where(x => x.Entity == "Order" && x.EntityId == order.Order.Id.ToString() && x.Action == "CreateOrder").ExecuteUpdateAsync(set => set.SetProperty(x => x.UserId, (Guid?)null));
        Assert.False(await existing.CanApproveAsync(order.Order.Id, Guid.NewGuid(), false, true, default));
        Assert.True(await existing.CanApproveAsync(order.Order.Id, Guid.NewGuid(), true, true, default));
    }

    [SqlServerFact]
    public async Task Rounded_sku_snapshots_leave_other_sku_cost_outstanding_and_reverse_exact_source_cost()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var a = await SetupAsync(client);
        var b = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Purchase, a.Supplier, a.Warehouse, null,
                [new(a.Product, 3, 0.005m), new(b.Product, 1, 1)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(a.Product, 3), new(b.Product, 1)]));
        var sale = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, a.Customer, a.Warehouse, null,
                [new(a.Product, 3, 10), new(b.Product, 1, 10)])));
        for (var i = 0; i < 3; i++)
            sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(a.Product, 1)]));
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(b.Product, 1)]));
        var bShipment = sale.Fulfillments.MaxBy(x => x.Id)!;
        for (var i = 0; i < 3; i++)
            sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(a.Product, 1)]));
        var finalA = sale.Fulfillments.Where(x => x.IsReturn).MaxBy(x => x.Id)!;
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={a.Customer}"))!.Items;
        Assert.Equal(1m, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        Assert.Equal(0.03m, invoices.Where(x => x.IsCredit).Sum(x => x.CostAmount));
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var aSources = await (from f in db.OrderFulfillments
                                  join l in db.Set<StockLine>() on f.StockDocumentId equals l.DocumentId
                                  where f.OrderId == sale.Order.Id && l.ProductId == a.Product
                                  select l.PostedSalesCost).ToArrayAsync();
            Assert.Equal(6, aSources.Length); Assert.All(aSources, value => Assert.Equal(0.01m, value));
        }
        using var reverseReturn = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{finalA.Id}/reverse", null);
        sale = await ReadAsync<OrderDetail>(reverseReturn);
        var reversal = Assert.Single(sale.Fulfillments, x => x.ReversesId == finalA.Id);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            Assert.Equal(0.01m, await db.Set<StockLine>().Where(x => x.DocumentId == reversal.StockDocumentId).Select(x => x.PostedSalesCost).SingleAsync());
            Assert.Equal(0.01m, await db.Set<StockLine>().Where(x => x.DocumentId == finalA.StockDocumentId).Select(x => x.PostedSalesCost).SingleAsync());
        }
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(a.Product, 1)]));
        using var reverseB = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{bShipment.Id}/reverse", null);
        sale = await ReadAsync<OrderDetail>(reverseB);
        invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={a.Customer}"))!.Items;
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        var bReversal = Assert.Single(sale.Fulfillments, x => x.ReversesId == bShipment.Id);
        Assert.Equal(1m, Assert.Single(invoices, x => x.StockDocumentId == bReversal.StockDocumentId).CostAmount);
        Assert.Equal(1m, Assert.Single(invoices, x => x.StockDocumentId == bShipment.StockDocumentId).CostAmount);
        Assert.Equal(3, (await BalanceAsync(client, a.Product)).OnHand);
        Assert.Equal(1, (await BalanceAsync(client, b.Product)).OnHand);
    }

    [SqlServerFact]
    public async Task Legacy_nullable_source_keeps_document_level_cost_and_new_activity_unattributed()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        var sale = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 2, 20)])));
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        var original = Assert.Single(sale.Fulfillments);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            // Only this test-created source emulates the nullable shape of a pre-migration posting.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE inventory.DocumentLines SET PostedSalesCost=NULL WHERE DocumentId={original.StockDocumentId}");
        }
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        var finalReturn = sale.Fulfillments.Where(x => x.IsReturn).MaxBy(x => x.Id)!;
        using var reverse = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{finalReturn.Id}/reverse", null);
        await ReadAsync<OrderDetail>(reverse);
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 1)]));
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(20m, Assert.Single(invoices, x => x.StockDocumentId == original.StockDocumentId).CostAmount);
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        await using var readScope = fixture.Services.CreateAsyncScope();
        var readDb = readScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var costs = await (from f in readDb.OrderFulfillments
                           join l in readDb.Set<StockLine>() on f.StockDocumentId equals l.DocumentId
                           where f.OrderId == sale.Order.Id
                           select l.PostedSalesCost).ToArrayAsync();
        Assert.Equal(5, costs.Length); Assert.All(costs, value => Assert.Null(value));
    }
    [SqlServerFact]
    public async Task Multi_sku_non_last_reversal_preserves_source_cents_and_returns_reconcile_order_totals()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var other = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null,
                [new(s.Product, 1, 0.005m), new(other.Product, 1, 0.005m)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1), new(other.Product, 1)]));
        var sale = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null,
                [new(s.Product, 1, 0.005m), new(other.Product, 1, 0.005m)])));
        sale = await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1)]));
        var first = Assert.Single(sale.Fulfillments);
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(other.Product, 1)]));
        using var reversal = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{first.Id}/reverse", null);
        sale = await ReadAsync<OrderDetail>(reversal);
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        var originalInvoice = Assert.Single(invoices, x => x.StockDocumentId == first.StockDocumentId);
        var reversingSource = Assert.Single(sale.Fulfillments, x => x.ReversesId == first.Id);
        var credit = Assert.Single(invoices, x => x.StockDocumentId == reversingSource.StockDocumentId);
        Assert.Equal(originalInvoice.Amount, credit.Amount);
        Assert.Equal(originalInvoice.CostAmount, credit.CostAmount);
        Assert.Equal(0.01m, invoices.Sum(x => x.IsCredit ? -x.Amount : x.Amount));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 1)]));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 0.5m)]));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(s.Product, 0.5m)]));
        await FulfillAsync(client, sale.Order.Id, new(Guid.NewGuid(), true, [new(other.Product, 1)]));
        invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.Amount : x.Amount));
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
        Assert.Equal(1, (await BalanceAsync(client, s.Product)).OnHand);
        Assert.Equal(1, (await BalanceAsync(client, other.Product)).OnHand);
    }
    [SqlServerFact]
    public async Task Create_replay_checks_payload_and_cancel_replay_has_no_new_audit_or_version()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var input = new OrderInput(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, "Replay", [new(s.Product, 2, 10)]);
        var order = await CreateAsync(client, input);
        Assert.Equal(order.Order.Id, (await CreateAsync(client, input)).Order.Id);
        using var changed = await client.PostAsJsonAsync("/api/v1/orders", input with { Lines = [new(s.Product, 2, 11)] });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<OrderService>();
            var error = await Assert.ThrowsAsync<ErpException>(() => service.TransitionAsync(order.Order.Id, "complete",
                new(order.Order.Version), new(null, "transition-regression"), CancellationToken.None));
            Assert.Equal("invalid_order_transition", error.Code);
        }
        var cancelled = await TransitionAsync(client, order, "cancel");
        var repeated = await TransitionAsync(client, order, "cancel");
        Assert.Equal(cancelled.Order.Version, repeated.Order.Version);
        Assert.Equal(OrderStatus.Cancelled, repeated.Order.Status);
        await using var readScope = fixture.Services.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var id = order.Order.Id.ToString();
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Entity == "Order" && x.EntityId == id && x.Action == "CreateOrder"));
        Assert.Equal(1, await db.AuditEntries.CountAsync(x => x.Entity == "Order" && x.EntityId == id && x.Action == "cancelOrder"));
        Assert.False(await db.OrderFulfillments.AnyAsync(x => x.OrderId == order.Order.Id));
    }
    [SqlServerFact]
    public async Task Linked_shipment_reversal_uses_order_entry_point_and_retry_preserves_source_totals()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        var s = await SetupAsync(client);
        var purchase = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Purchase, s.Supplier, s.Warehouse, null, [new(s.Product, 2, 10)])));
        await FulfillAsync(client, purchase.Order.Id, new(Guid.NewGuid(), false, [new(s.Product, 2)]));
        var sale = await ApproveAsync(client, await CreateAsync(client,
            new(Guid.NewGuid(), OrderKind.Sales, s.Customer, s.Warehouse, null, [new(s.Product, 1, 20)])));
        var input = new FulfillmentInput(Guid.NewGuid(), false, [new(s.Product, 1)]);
        sale = await FulfillAsync(client, sale.Order.Id, input);
        var shipment = Assert.Single(sale.Fulfillments);
        using var direct = await client.PostAsync($"/api/v1/inventory/documents/{shipment.StockDocumentId}/reverse", null);
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode);
        using var changed = await client.PostAsJsonAsync($"/api/v1/orders/{sale.Order.Id}/fulfill", input with { IsReturn = true });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var reverse = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{shipment.Id}/reverse", null);
        var reversed = await ReadAsync<OrderDetail>(reverse);
        using var retry = await client.PostAsync($"/api/v1/orders/{sale.Order.Id}/fulfillments/{shipment.Id}/reverse", null);
        Assert.Equal(reversed.Order.Version, (await ReadAsync<OrderDetail>(retry)).Order.Version);
        Assert.Equal(0, reversed.Lines[0].FulfilledQuantity);
        var balance = await BalanceAsync(client, s.Product);
        Assert.Equal(2, balance.OnHand); Assert.Equal(1, balance.Reserved);
        var invoices = (await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}"))!.Items;
        Assert.Equal(2, invoices.Count);
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.Amount : x.Amount));
        Assert.Equal(0, invoices.Sum(x => x.IsCredit ? -x.CostAmount : x.CostAmount));
    }
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
        Assert.Equal(6.67m, Assert.Single(invoices, x => x.StockDocumentId == finalFraction.StockDocumentId).CostAmount);
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
    { using var r = await client.PostAsJsonAsync($"/api/v1/orders/{order.Order.Id}/{action}", new OrderVersion(order.Order.Version, action == "cancel" ? "Test cancellation" : null)); return await ReadAsync<OrderDetail>(r); }
    private static async Task<OrderDetail> ApproveAsync(HttpClient client, OrderDetail order) => await TransitionAsync(client, await TransitionAsync(client, order, "submit"), "approve");
    private static async Task<OrderDetail> FulfillAsync(HttpClient client, int id, FulfillmentInput input)
    { using var r = await client.PostAsJsonAsync($"/api/v1/orders/{id}/fulfill", input); return await ReadAsync<OrderDetail>(r); }
    private static async Task<StockBalanceRow> BalanceAsync(HttpClient client, Guid product) =>
        Assert.Single((await client.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?productId={product}"))!.Items);
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    { Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}"); return (await response.Content.ReadFromJsonAsync<T>())!; }
}
