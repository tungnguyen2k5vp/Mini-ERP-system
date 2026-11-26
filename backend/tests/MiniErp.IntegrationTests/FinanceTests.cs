using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Finance;
using MiniErp.Application.Auth;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class FinanceTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Partial_multi_invoice_payments_retry_overpayment_refund_and_reports_reconcile()
    {
        var (partner, method, invoices) = await SeedInvoicesAsync(100m, 60m);
        using var client = await fixture.AuthenticatedClientAsync();
        var input = new PaymentInput(Guid.NewGuid(), partner, FinanceDirection.Receivable, false, method, 70m, "bank-1",
            [new(invoices[0], 40m), new(invoices[1], 30m)]);
        using var response = await client.PostAsJsonAsync("/api/v1/finance/payments", input);
        var payment = await ReadAsync<PaymentRow>(response);
        using var repeat = await client.PostAsJsonAsync("/api/v1/finance/payments", input with { Allocations = input.Allocations.Reverse().ToArray() });
        Assert.Equal(payment.Id, (await ReadAsync<PaymentRow>(repeat)).Id);
        using var reused = await client.PostAsJsonAsync("/api/v1/finance/payments", input with { Reference = "changed" });
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        using var over = await client.PostAsJsonAsync("/api/v1/finance/payments", input with {
            RequestId = Guid.NewGuid(), Amount = 61m, Allocations = [new(invoices[0], 61m)] });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        using var settlement=await client.PostAsJsonAsync("/api/v1/finance/payments",input with {
            RequestId=Guid.NewGuid(),Amount=90m,Allocations=[new(invoices[0],60m),new(invoices[1],30m)]});
        await ReadAsync<PaymentRow>(settlement);
        var credit=await AddSourceAsync(partner,20m,true);
        using var refund = await client.PostAsJsonAsync("/api/v1/finance/payments", input with {
            RequestId = Guid.NewGuid(), IsRefund = true, Amount = 10m, Allocations = [new(credit, 10m)] });
        Assert.True(refund.IsSuccessStatusCode, await refund.Content.ReadAsStringAsync());
        var rows = await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.Equal(0m, Assert.Single(rows!.Items, x => x.Id == invoices[0]).Outstanding);
        Assert.Equal(0m, Assert.Single(rows.Items, x => x.Id == invoices[1]).Outstanding);
        Assert.Equal(10m, Assert.Single(rows.Items, x => x.Id == credit).Outstanding);
        var aging = await client.GetFromJsonAsync<AgingRow[]>("/api/v1/finance/aging");
        Assert.Equal(-10m, Assert.Single(aging!, x => x.PartnerId == partner).Balance);
        var reconciliation = await client.GetFromJsonAsync<ReconciliationRow[]>("/api/v1/finance/reconciliation");
        Assert.All(reconciliation!, row => Assert.Equal(0m, row.Difference));
    }

    [SqlServerFact]
    public async Task Concurrent_payments_cannot_overallocate_and_anonymous_access_is_denied()
    {
        var (partner, method, ids) = await SeedInvoicesAsync(100m);
        using var a = await fixture.AuthenticatedClientAsync();
        using var b = await fixture.AuthenticatedClientAsync();
        var input = new PaymentInput(Guid.NewGuid(), partner, FinanceDirection.Receivable, false, method, 80m, null, [new(ids[0], 80m)]);
        var results = await Task.WhenAll(a.PostAsJsonAsync("/api/v1/finance/payments", input),
            b.PostAsJsonAsync("/api/v1/finance/payments", input with { RequestId = Guid.NewGuid() }));
        try
        {
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var result in results) result.Dispose(); }
        using var anonymous = fixture.NewClient();
        using var denied = await anonymous.GetAsync("/api/v1/finance/invoices");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var rows = await a.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.Equal(20m, Assert.Single(rows!.Items).Outstanding);
    }

    [SqlServerFact]
    public async Task Finance_permissions_block_viewer_reads_and_manager_payment_posts()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        foreach (var role in new[] { "Viewer", "Manager" })
        {
            var email = $"finance-{Guid.NewGuid():N}@minierp.example";
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa7!";
            using var account = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Finance policy", password, role));
            Assert.Equal(HttpStatusCode.Created, account.StatusCode);
            using var user = await fixture.AuthenticatedClientAsync(email, password);
            using var read = await user.GetAsync("/api/v1/finance/invoices");
            Assert.Equal(role == "Viewer" ? HttpStatusCode.Forbidden : HttpStatusCode.OK, read.StatusCode);
            using var denied = await user.PostAsJsonAsync("/api/v1/finance/payments",
                new PaymentInput(Guid.NewGuid(), Guid.NewGuid(), FinanceDirection.Receivable, false, Guid.NewGuid(), 1m, null, [new(1, 1m)]));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Returns_before_cash_offset_debits_and_close_after_only_net_payment()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        var credit=await AddSourceAsync(partner,40m,true);
        using var client=await fixture.AuthenticatedClientAsync();
        var input=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,61m,null,[new(ids[0],61m)]);
        using var over=await client.PostAsJsonAsync("/api/v1/finance/payments",input);
        Assert.Equal(HttpStatusCode.Conflict,over.StatusCode);
        using var uncashRefund=await client.PostAsJsonAsync("/api/v1/finance/payments",input with{RequestId=Guid.NewGuid(),IsRefund=true,Amount=1m,Allocations=[new(credit,1m)]});
        Assert.Equal(HttpStatusCode.Conflict,uncashRefund.StatusCode);
        var before=await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.Equal(60m,Assert.Single(before!.Items,x=>!x.IsCredit).ActionableAmount);
        Assert.Equal(0m,Assert.Single(before.Items,x=>x.IsCredit).ActionableAmount);
        using var paid=await client.PostAsJsonAsync("/api/v1/finance/payments",input with{Amount=60m,Allocations=[new(ids[0],60m)]});
        await ReadAsync<PaymentRow>(paid);
        var closed=await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.All(closed!.Items,x=>{Assert.Equal(0m,x.Outstanding);Assert.Equal(0m,x.ActionableAmount);Assert.Equal(x.Amount,x.Settled+x.OffsetAmount+x.Outstanding);});
        var aging=await client.GetFromJsonAsync<AgingRow[]>("/api/v1/finance/aging");
        Assert.Equal(0m,Assert.Single(aging!,x=>x.PartnerId==partner).Balance);
    }

    [SqlServerFact]
    public async Task Fully_returned_unpaid_order_cannot_accept_payment_or_refund_even_with_excess_credit()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        var credit=await AddSourceAsync(partner,100m,true);
        using var client=await fixture.AuthenticatedClientAsync();
        var input=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,1m,null,[new(ids[0],1m)]);
        using var paid=await client.PostAsJsonAsync("/api/v1/finance/payments",input);
        Assert.Equal(HttpStatusCode.Conflict,paid.StatusCode);
        using var refunded=await client.PostAsJsonAsync("/api/v1/finance/payments",input with{RequestId=Guid.NewGuid(),IsRefund=true,Allocations=[new(credit,1m)]});
        Assert.Equal(HttpStatusCode.Conflict,refunded.StatusCode);
        var rows=await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.All(rows!.Items,x=>Assert.Equal(0m,x.Outstanding));
        var extraCredit=await AddSourceAsync(partner,50m,true);
        using var excess=await client.PostAsJsonAsync("/api/v1/finance/payments",input with{RequestId=Guid.NewGuid(),IsRefund=true,Allocations=[new(extraCredit,1m)]});
        Assert.Equal(HttpStatusCode.Conflict,excess.StatusCode);
    }

    [SqlServerFact]
    public async Task Negative_net_invoice_limits_refunds_to_actual_cash_and_prevents_second_refund()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        using var client=await fixture.AuthenticatedClientAsync();
        var input=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,30m,null,[new(ids[0],30m)]);
        using var paid=await client.PostAsJsonAsync("/api/v1/finance/payments",input); await ReadAsync<PaymentRow>(paid);
        var credit=await AddSourceAsync(partner,150m,true);
        using var over=await client.PostAsJsonAsync("/api/v1/finance/payments",input with{RequestId=Guid.NewGuid(),IsRefund=true,Amount=31m,Allocations=[new(credit,31m)]});
        Assert.Equal(HttpStatusCode.Conflict,over.StatusCode);
        var refund=input with{RequestId=Guid.NewGuid(),IsRefund=true,Allocations=[new(credit,30m)]};
        using var refunded=await client.PostAsJsonAsync("/api/v1/finance/payments",refund); await ReadAsync<PaymentRow>(refunded);
        using var twice=await client.PostAsJsonAsync("/api/v1/finance/payments",refund with{RequestId=Guid.NewGuid(),Amount=1m,Allocations=[new(credit,1m)]});
        Assert.Equal(HttpStatusCode.Conflict,twice.StatusCode);
    }

    [SqlServerFact]
    public async Task Competing_multi_order_allocations_commit_all_lines_or_none()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        var shared=await AddSourceAsync(partner,100m,false,true);
        var third=await AddSourceAsync(partner,100m,false,true);
        using var a=await fixture.AuthenticatedClientAsync(); using var b=await fixture.AuthenticatedClientAsync();
        var first=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,160m,null,[new(ids[0],80m),new(shared,80m)]);
        var second=first with{RequestId=Guid.NewGuid(),Amount=120m,Allocations=[new(shared,60m),new(third,60m)]};
        var responses=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/finance/payments",first),b.PostAsJsonAsync("/api/v1/finance/payments",second));
        try
        {
            Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK); Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
            var committed=await ReadAsync<PaymentRow>(responses.Single(x=>x.StatusCode==HttpStatusCode.OK));
            var rows=await a.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
            Assert.Equal(committed.Amount,rows!.Items.Sum(x=>x.Settled));
            Assert.Equal(300m-committed.Amount,rows.Items.Sum(x=>x.Outstanding));
            Assert.Equal(0m,Assert.Single(rows.Items,x=>x.Id==(committed.RequestId==first.RequestId?third:ids[0])).Settled);
        }
        finally{foreach(var response in responses)response.Dispose();}
    }

    [SqlServerFact]
    public async Task Return_and_payment_share_order_serialization_and_keep_refund_backed_by_cash()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        using var client=await fixture.AuthenticatedClientAsync();
        var input=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,80m,null,[new(ids[0],80m)]);
        var returning=AddSourceAsync(partner,40m,true);
        var paying=client.PostAsJsonAsync("/api/v1/finance/payments",input);
        await Task.WhenAll(returning,paying);
        using var payment=await paying;
        Assert.True(payment.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict,await payment.Content.ReadAsStringAsync());
        var rows=await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        var cash=payment.StatusCode==HttpStatusCode.OK?80m:0m;
        Assert.Equal(cash,rows!.Items.Sum(x=>x.Settled));
        Assert.Equal(Math.Max(0,cash-60m),Assert.Single(rows.Items,x=>x.IsCredit).ActionableAmount);
        Assert.Equal(Math.Max(0,60m-cash),Assert.Single(rows.Items,x=>!x.IsCredit).ActionableAmount);
    }

    [SqlServerFact]
    public async Task Competing_credit_invoices_cannot_refund_the_same_cash_twice()
    {
        var(partner,method,ids)=await SeedInvoicesAsync(100m);
        using var a=await fixture.AuthenticatedClientAsync(); using var b=await fixture.AuthenticatedClientAsync();
        var input=new PaymentInput(Guid.NewGuid(),partner,FinanceDirection.Receivable,false,method,100m,null,[new(ids[0],100m)]);
        using var paid=await a.PostAsJsonAsync("/api/v1/finance/payments",input); await ReadAsync<PaymentRow>(paid);
        var first=await AddSourceAsync(partner,80m,true); var second=await AddSourceAsync(partner,80m,true);
        var refund=input with{RequestId=Guid.NewGuid(),IsRefund=true,Amount=80m,Allocations=[new(first,80m)]};
        var responses=await Task.WhenAll(a.PostAsJsonAsync("/api/v1/finance/payments",refund),
            b.PostAsJsonAsync("/api/v1/finance/payments",refund with{RequestId=Guid.NewGuid(),Allocations=[new(second,80m)]}));
        try{Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);}
        finally{foreach(var response in responses)response.Dispose();}
        var rows=await a.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={partner}");
        Assert.Equal(80m,rows!.Items.Where(x=>x.IsCredit).Sum(x=>x.Settled));
        Assert.Equal(20m,rows.Items.Where(x=>x.IsCredit).Sum(x=>x.ActionableAmount));
    }

    private async Task<long> AddSourceAsync(Guid partner,decimal amount,bool credit,bool newOrder=false)
    {
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var original=await db.Orders.AsNoTracking().FirstAsync(x=>x.PartnerId==partner);
        var order=original;
        var at=DateTime.UtcNow;
        if(newOrder)
        {
            order=new TradeOrder{RequestId=Guid.NewGuid(),Kind=OrderKind.Sales,PartnerId=partner,WarehouseId=original.WarehouseId,CreatedAtUtc=at,UpdatedAtUtc=at};
            db.Add(order); await db.SaveChangesAsync();
        }
        await using var transaction=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [orders].[Orders] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={order.Id}");
        var source=new StockDocument{RequestId=Guid.NewGuid(),Kind=credit?StockKind.Receipt:StockKind.Issue,WarehouseId=order.WarehouseId,CreatedAtUtc=at};
        source.Post(at); db.Add(source); await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IFinancePosting>().AddInvoiceAsync(new(source.Id,order.Id,"FIN-"+order.Id,partner,FinanceDirection.Receivable,credit,amount,0m,at),CancellationToken.None);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return await db.Set<FinanceInvoice>().Where(x=>x.StockDocumentId==source.Id).Select(x=>x.Id).SingleAsync();
    }

    private async Task<(Guid Partner, Guid Method, long[] Invoices)> SeedInvoicesAsync(params decimal[] amounts)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var code = Guid.NewGuid().ToString("N")[..12];
        var partner = new BusinessPartner { Kind = PartnerKind.Customer, Code = code, Name = "Finance " + code };
        var warehouse = new CatalogLookup { Kind = LookupKind.Warehouse, Code = code, Name = code };
        var method = new CatalogLookup { Kind = LookupKind.PaymentMethod, Code = code, Name = code };
        db.AddRange(partner, warehouse, method);
        var at = DateTime.UtcNow;
        var order = new TradeOrder { RequestId = Guid.NewGuid(), Kind = OrderKind.Sales, PartnerId = partner.Id,
            WarehouseId = warehouse.Id, CreatedAtUtc = at, UpdatedAtUtc = at };
        db.Add(order);
        await db.SaveChangesAsync();
        var sources = new List<StockDocument>();
        foreach (var _ in amounts)
        {
            var source = new StockDocument { RequestId = Guid.NewGuid(), Kind = StockKind.Issue, WarehouseId = warehouse.Id, CreatedAtUtc = at };
            source.Post(at);
            db.Add(source);
            sources.Add(source);
        }
        await db.SaveChangesAsync();
        var posting = scope.ServiceProvider.GetRequiredService<IFinancePosting>();
        for (var i = 0; i < amounts.Length; i++)
            await posting.AddInvoiceAsync(new(sources[i].Id, order.Id, "FIN-" + order.Id, partner.Id, FinanceDirection.Receivable,
                false, amounts[i], 0m, at), CancellationToken.None);
        await db.SaveChangesAsync();
        return (partner.Id, method.Id, await db.Set<FinanceInvoice>().Where(x => x.PartnerId == partner.Id).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
