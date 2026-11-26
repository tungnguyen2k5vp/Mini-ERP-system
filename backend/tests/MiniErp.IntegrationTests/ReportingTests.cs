using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Application.Reporting;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class ReportingTests(ErpFixture fixture)
{
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
        var today=DateTime.UtcNow.ToString("yyyy-MM-dd");
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
        var start=DateTime.UtcNow.Date; var until=start.AddDays(1);
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
        var search=await viewer.GetFromJsonAsync<SearchHit[]>("/api/v1/search?q=Test");
        Assert.All(search!,x=>Assert.True(x.Kind is "product" or "stock" or "customer" or "supplier"));
        using var audit=await viewer.GetAsync("/api/v1/audit"); Assert.Equal(HttpStatusCode.Forbidden,audit.StatusCode);
        using var workbook=await viewer.GetAsync("/api/v1/catalog/products/workbook"); Assert.Equal(HttpStatusCode.Forbidden,workbook.StatusCode);
        using var invalid=await viewer.GetAsync("/api/v1/reports/stock?from=2026-02-02&to=2026-01-01"); Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }
}
