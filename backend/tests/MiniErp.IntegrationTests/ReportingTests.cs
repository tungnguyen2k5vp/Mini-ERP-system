using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Finance;
using MiniErp.Application.Inventory;
using MiniErp.Application.Orders;
using MiniErp.Application.Reporting;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Orders;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Reporting;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class ReportingTests(ErpFixture fixture)
{
    private sealed class PostingClock(DateTime utc) : TimeProvider
    {
        public DateTime Utc { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Utc);
    }

    [SqlServerFact]
    public async Task Vietnam_new_year_includes_last_tick_and_attributes_each_split_line_to_its_source()
    {
        using var client = await fixture.AuthenticatedClientAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var last = await db.StockDocuments.MaxAsync(d => d.PostedAtUtc);
        var year = Math.Max(2100, (last?.Year ?? 0) + 2);
        var start = new DateTime(year - 1, 12, 31, 17, 0, 0, DateTimeKind.Utc);
        var until = start.AddDays(1);
        var clock = new PostingClock(start.AddTicks(-1));
        var stock = new StockService(db, clock);
        var orders = new OrderService(db, stock, new FinanceService(db, clock), clock);
        var code = Guid.NewGuid().ToString("N")[..10];
        var actor = new RequestActor(null, code);
        var ct = CancellationToken.None;
        CatalogLookup Lookup(LookupKind kind, string suffix) => new() { Kind = kind, Code = code + suffix, Name = code + suffix };
        var origin = Lookup(LookupKind.Warehouse, "W");
        var other = Lookup(LookupKind.Warehouse, "D");
        var category = Lookup(LookupKind.Category, "C");
        var piece = Lookup(LookupKind.Unit, "U");
        var box = Lookup(LookupKind.Unit, "B");
        var customer = new BusinessPartner { Kind = PartnerKind.Customer, Code = code, Name = code };
        Product Item(CatalogLookup unit) => new() { Sku = unit.Code, Name = unit.Name, Category = category,
            Unit = unit, CreatedAtUtc = clock.Utc, UpdatedAtUtc = clock.Utc };
        var first = Item(piece);
        var second = Item(box);
        db.AddRange(origin, other, first, second, customer);
        await db.SaveChangesAsync(ct);
        // SQL uniqueidentifier ordering matters: a first-line residual would charge the wrong SKU.
        var ids = await db.Products.Where(p => p.Id == first.Id || p.Id == second.Id).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync(ct);
        var whole = ids[0];
        var fraction = ids[1];
        async Task PostStock(StockKind kind, Guid? destination, params StockLineInput[] lines)
        {
            var draft = await stock.CreateAsync(new(Guid.NewGuid(),kind,origin.Id,destination,null,lines),actor,ct);
            await stock.PostAsync(draft.Document.Id,actor,ct);
        }
        await PostStock(StockKind.Receipt, null, new(whole,10,1), new(fraction,10,1));
        var due = BusinessCalendar.DateAt(clock.Utc);
        var sale = await orders.CreateAsync(new(Guid.NewGuid(),OrderKind.Sales,customer.Id,origin.Id,null,
            [new(whole,1,1),new(fraction,2,0.005m)], due),actor,ct);
        sale = await orders.TransitionAsync(sale.Order.Id,"submit",new(sale.Order.Version),actor,ct);
        sale = await orders.TransitionAsync(sale.Order.Id,"approve",new(sale.Order.Version),actor,ct,canSelfApprove: true);
        Assert.False(sale.Order.IsOverdue);
        await orders.FulfillAsync(sale.Order.Id,new(Guid.NewGuid(),false,[new(fraction,1)]),actor,ct);
        clock.Utc = start;
        Assert.True((await orders.DetailAsync(sale.Order.Id, ct)).Order.IsOverdue);
        var open = await orders.ListAsync(OrderKind.Sales, null, 1, 15, ct, onlyOverdue: true);
        var queue = await new OperationsReport(db, clock).DashboardAsync(due, due,
            new HashSet<string> { Permissions.ViewSales }, ct);
        Assert.Equal(open.Total, Assert.Single(queue.Alerts, a => a.Href == "/ban-hang?overdue=true").Count);
        Assert.DoesNotContain(queue.Alerts, a => a.Href.StartsWith("/mua-hang", StringComparison.Ordinal));
        await orders.FulfillAsync(sale.Order.Id,new(Guid.NewGuid(),false,[new(whole,1),new(fraction,1)]),actor,ct);
        Assert.False((await orders.DetailAsync(sale.Order.Id, ct)).Order.IsOverdue);
        clock.Utc = until.AddTicks(-1);
        sale = await orders.FulfillAsync(sale.Order.Id,new(Guid.NewGuid(),true,[new(whole,1)]),actor,ct);
        await orders.ReverseAsync(sale.Order.Id, sale.Fulfillments.Single(f => f.IsReturn).Id, actor, ct);
        await PostStock(StockKind.Transfer, other.Id, new StockLineInput(whole,1,null));
        clock.Utc = until;
        await PostStock(StockKind.Receipt,null,new StockLineInput(whole,100,1));

        var period = $"from={year}-01-01&to={year}-01-01";
        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>($"/api/v1/reports/dashboard?{period}",ct))!;
        var activity = Assert.Single(dashboard.Trend);
        Assert.Equal(new DateTime(year,1,1),activity.Day);
        Assert.Equal((1,1),(activity.ReceiptCount,activity.IssueCount));
        var profit = Assert.Single(dashboard.ProfitTrend);
        Assert.Equal(activity.Day,profit.Day);
        Assert.Equal((1m,2m,-1m),(profit.Revenue,profit.Cost,profit.GrossProfit));
        Assert.Equal(0m,Assert.Single(dashboard.TopProducts,p=>p.ProductId==fraction).Revenue);
        Assert.Equal(1m,Assert.Single(dashboard.TopProducts,p=>p.ProductId==whole).Revenue);
        Assert.Equal(profit.Revenue,dashboard.TopProducts.Sum(p=>p.Revenue));
        var positions = (await client.GetFromJsonAsync<Page<StockReportRow>>($"/api/v1/reports/stock?{period}&warehouseId={origin.Id}",ct))!.Items;
        var boxed = Assert.Single(positions,p=>p.ProductId==whole);
        Assert.Equal((10m,1m,3m,8m),(boxed.Opening,boxed.Received,boxed.Issued,boxed.Closing));
        var split = Assert.Single(positions,p=>p.ProductId==fraction);
        Assert.Equal((9m,0m,1m,8m),(split.Opening,split.Received,split.Issued,split.Closing));
        var events = (await client.GetFromJsonAsync<Page<AuditRow>>($"/api/v1/audit?{period}&search={code}&pageSize=100",ct))!.Items;
        Assert.Contains(events,e=>e.TimestampUtc==start);
        Assert.Contains(events,e=>e.TimestampUtc==until.AddTicks(-1));
        Assert.All(events,e=>Assert.InRange(e.TimestampUtc,start,until.AddTicks(-1)));
    }

    [SqlServerFact]
    public async Task Dashboard_and_stock_reconcile_posted_orders_returns_and_finance_sources()
    {
        using var client=await fixture.AuthenticatedClientAsync();
        var code=Guid.NewGuid().ToString("N")[..12];
        async Task<T> Post<T>(string path,object input)
        {
            using var response=await client.PostAsJsonAsync("/api/v1/"+path,input);
            Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        var warehouse=await Post<LookupResponse>("warehouses",new LookupInput(code+"W",code,true,null));
        var category=await Post<LookupResponse>("categories",new LookupInput(code+"C",code,true,null));
        var unit=await Post<LookupResponse>("units",new LookupInput(code+"U",code,true,null));
        var product=await Post<ProductResponse>("products",new ProductInput(code,code,category.Id,unit.Id,100,200,2,null,null,true,null));
        var supplier=await Post<PartnerResponse>("suppliers",new PartnerInput(code,code,null,null,null,null,0,30,true,null));
        var customer=await Post<PartnerResponse>("customers",new PartnerInput(code,code,null,null,null,null,0,30,true,null));
        async Task<OrderDetail> Order(OrderKind kind,Guid partner,int quantity,decimal price)
        {
            var result=await Post<OrderDetail>("orders",new OrderInput(Guid.NewGuid(),kind,partner,warehouse.Id,null,[new(product.Id,quantity,price)]));
            result=await Post<OrderDetail>($"orders/{result.Order.Id}/submit",new OrderVersion(result.Order.Version));
            result=await Post<OrderDetail>($"orders/{result.Order.Id}/approve",new OrderVersion(result.Order.Version));
            return await Post<OrderDetail>($"orders/{result.Order.Id}/fulfill",new FulfillmentInput(Guid.NewGuid(),false,[new(product.Id,quantity)]));
        }
        await Order(OrderKind.Purchase,supplier.Id,10,100);
        var sale=await Order(OrderKind.Sales,customer.Id,4,200);
        await Post<OrderDetail>($"orders/{sale.Order.Id}/fulfill",new FulfillmentInput(Guid.NewGuid(),true,[new(product.Id,1)]));
        var businessDay=BusinessCalendar.DateAt(DateTime.UtcNow);
        var today=businessDay.ToString("yyyy-MM-dd");
        var stock=await client.GetFromJsonAsync<Page<StockReportRow>>($"/api/v1/reports/stock?from={today}&to={today}&warehouseId={warehouse.Id}");
        var row=Assert.Single(stock!.Items);
        Assert.Equal(0m,row.Opening); Assert.Equal(11m,row.Received); Assert.Equal(4m,row.Issued); Assert.Equal(7m,row.Closing);
        Assert.Equal(row.Opening+row.Received-row.Issued,row.Closing);
        var invoices=await client.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={customer.Id}");
        Assert.Equal(600m,invoices!.Items.Sum(x=>x.Amount*(x.IsCredit?-1:1)));
        Assert.Equal(300m,invoices.Items.Sum(x=>x.CostAmount*(x.IsCredit?-1:1)));
        var dashboard=await client.GetFromJsonAsync<DashboardResponse>($"/api/v1/reports/dashboard?from={today}&to={today}");
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var start=BusinessCalendar.StartUtc(businessDay); var until=start.AddDays(1);
        var posted=await db.Set<FinanceInvoice>().Where(x=>x.Direction==FinanceDirection.Receivable && x.IssuedAtUtc>=start && x.IssuedAtUtc<until).ToArrayAsync();
        Assert.Equal(posted.Sum(x=>x.Amount*(x.IsCredit?-1:1)),Assert.Single(dashboard!.Metrics,x=>x.Key=="revenue").Value);
        Assert.Equal(posted.Sum(x=>(x.Amount-x.CostAmount)*(x.IsCredit?-1:1)),Assert.Single(dashboard.Metrics,x=>x.Key=="grossProfit").Value);
        Assert.All(dashboard.Metrics.Where(x=>x.Key is "revenue" or "grossProfit" or "costOfGoods"),x=>Assert.Equal("/tong-quan#financial-report",x.Href));
        Assert.Contains(dashboard.Alerts,x=>x.Label=="Đơn mua chờ phê duyệt" && x.Href=="/mua-hang?status=2");
        Assert.Contains(dashboard.Alerts,x=>x.Label=="Đơn bán chờ phê duyệt" && x.Href=="/ban-hang?status=2");
        var hits=await client.GetFromJsonAsync<SearchHit[]>("/api/v1/search?q="+code);
        Assert.Contains(hits!,x=>x.Kind=="customer" && x.Href=="/khach-hang/"+customer.Id);
        Assert.Contains(hits!,x=>x.Kind=="supplier" && x.Href=="/nha-cung-cap/"+supplier.Id);
        var invoiceHit=hits!.First(x=>x.Kind=="invoice");
        var filtered=await client.GetFromJsonAsync<Page<InvoiceRow>>(invoiceHit.Href.Replace("/tai-chinh?","/api/v1/finance/invoices?"));
        Assert.Equal(1,filtered!.Total); Assert.Single(filtered.Items);
    }

    [SqlServerFact]
    public async Task Report_permissions_remove_financial_data_and_search_results_and_block_audit()
    {
        using var admin=await fixture.AuthenticatedClientAsync();
        var email=$"report-{Guid.NewGuid():N}@minierp.example";
        var password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))+"Aa7!";
        using var account=await admin.PostAsJsonAsync("/api/v1/users",new CreateUserRequest(email,"Report viewer",password,"Viewer"));
        Assert.Equal(HttpStatusCode.Created,account.StatusCode);
        using var viewer=await fixture.AuthenticatedClientAsync(email,password);
        var dashboard=await viewer.GetFromJsonAsync<DashboardResponse>("/api/v1/reports/dashboard");
        Assert.DoesNotContain(dashboard!.Metrics,x=>new[]{"revenue","grossProfit","costOfGoods","receivable","payable","salesOrders","purchaseOrders"}.Contains(x.Key));
        Assert.Empty(dashboard.TopProducts); Assert.Empty(dashboard.ProfitTrend);
        Assert.DoesNotContain(dashboard.Alerts,x=>x.Label.Contains("phê duyệt",StringComparison.Ordinal));
        Assert.DoesNotContain(dashboard.Alerts,x=>x.Href.Contains("overdue=true",StringComparison.Ordinal));
        var search=await viewer.GetFromJsonAsync<SearchHit[]>("/api/v1/search?q=Test");
        Assert.All(search!,x=>Assert.True(x.Kind is "product" or "stock" or "customer" or "supplier"));
        using var audit=await viewer.GetAsync("/api/v1/audit"); Assert.Equal(HttpStatusCode.Forbidden,audit.StatusCode);
        using var workbook=await viewer.GetAsync("/api/v1/catalog/products/workbook"); Assert.Equal(HttpStatusCode.Forbidden,workbook.StatusCode);
        using var invalid=await viewer.GetAsync("/api/v1/reports/stock?from=2026-02-02&to=2026-01-01"); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }
}
