using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Xml.Linq;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Finance;
using MiniErp.Application.Inventory;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;
using MiniErp.Domain.Inventory;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class SecurityBoundaryTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Ordinary_roles_keep_operational_quantities_without_unauthorized_costs_or_purchase_contracts()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(admin);
        foreach (var role in new[] { "Viewer", "Warehouse Staff", "Sales Staff", "Purchasing Staff", "Manager", "Accountant" })
        {
            using var user = await UserAsync(admin, role);
            var product = await user.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{s.Product}");
            var purchasePrices = role is "Purchasing Staff" or "Manager" or "Accountant";
            Assert.Equal(purchasePrices ? 123.45m : (decimal?)null, product!.PurchasePrice);
            var listed = await user.GetFromJsonAsync<Page<ProductResponse>>($"/api/v1/products?search={s.Code}");
            Assert.Equal(product.PurchasePrice, Assert.Single(listed!.Items).PurchasePrice);
            if (role != "Accountant")
            {
                var balance = Assert.Single((await user.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?productId={s.Product}"))!.Items);
                Assert.Equal(3m, balance.OnHand);
                Assert.Equal(role == "Manager" ? 123.45m : (decimal?)null, balance.AverageCost);
                var movements = await user.GetFromJsonAsync<Page<StockMovementRow>>($"/api/v1/inventory/history?productId={s.Product}");
                Assert.All(movements!.Items, x => Assert.Equal(role == "Manager" ? 123.45m : (decimal?)null, x.UnitCost));
                var doc = await user.GetFromJsonAsync<StockDocumentResponse>($"/api/v1/inventory/documents/{s.StockId}");
                Assert.Equal(role == "Manager" ? 123.45m : (decimal?)null, Assert.Single(doc!.Lines).UnitCost);
                var options = await user.GetFromJsonAsync<StockProductOption[]>("/api/v1/inventory/product-options");
                Assert.Equal(product.PurchasePrice, Assert.Single(options!, x => x.Id == s.Product).PurchasePrice);
            }
            if (role is "Warehouse Staff" or "Purchasing Staff" or "Manager" or "Accountant")
            {
                var purchase = await user.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{s.Purchase}");
                Assert.Equal(purchasePrices ? 123.45m : (decimal?)null, Assert.Single(purchase!.Lines).UnitPrice);
                Assert.Equal(purchasePrices ? 493.8m : (decimal?)null, purchase.Order.Total);
                Assert.Equal(purchasePrices ? 493.8m : (decimal?)null, Assert.Single(purchase.Fulfillments).Amount);
                var options = await user.GetFromJsonAsync<OrderOptions>("/api/v1/orders/options?kind=1");
                Assert.Equal(product.PurchasePrice, Assert.Single(options!.Products, x => x.Id == s.Product).PurchasePrice);
                using var sorted = await user.GetAsync("/api/v1/orders?kind=1&sort=total");
                Assert.Equal(purchasePrices ? HttpStatusCode.OK : HttpStatusCode.Forbidden, sorted.StatusCode);
            }
            if (role is "Sales Staff" or "Warehouse Staff" or "Manager" or "Accountant")
            {
                var sale = await user.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{s.Sale}");
                Assert.Equal(200m, Assert.Single(sale!.Lines).UnitPrice);
                var options = await user.GetFromJsonAsync<OrderOptions>("/api/v1/orders/options?kind=2");
                Assert.Equal(product.PurchasePrice, Assert.Single(options!.Products, x => x.Id == s.Product).PurchasePrice);
            }
            if (role is "Manager" or "Accountant")
            {
                var invoices = await user.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}");
                Assert.Equal(123.45m, Assert.Single(invoices!.Items).CostAmount);
            }
            else
            {
                using var denied = await user.GetAsync("/api/v1/finance/invoices");
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }
        using var warehouseUser = await UserAsync(admin, "Warehouse Staff");
        var position = Assert.Single((await warehouseUser.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?productId={s.Product}"))!.Items);
        using var draftResponse = await warehouseUser.PostAsJsonAsync("/api/v1/inventory/documents",
            new StockDocumentInput(Guid.NewGuid(), StockKind.Issue, position.WarehouseId, null, null, [new(s.Product, 1, null)]));
        var draft = await ReadAsync<StockDocumentResponse>(draftResponse);
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        Assert.NotNull(draftResponse.Headers.Location);
        using var postedResponse = await warehouseUser.PostAsync($"/api/v1/inventory/documents/{draft.Document.Id}/post", null);
        var posted = await ReadAsync<StockDocumentResponse>(postedResponse);
        Assert.Equal(StockStatus.Posted, posted.Document.Status);
        Assert.Null(Assert.Single(posted.Lines).UnitCost);
        Assert.Equal(2m, Assert.Single((await warehouseUser.GetFromJsonAsync<Page<StockBalanceRow>>($"/api/v1/inventory/balances?productId={s.Product}"))!.Items).OnHand);
    }

    [SqlServerFact]
    public async Task Revoked_sensitive_grant_applies_to_existing_token_and_export_and_catalog_editor_keeps_prices()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var s = await SeedAsync(admin);
        var original = Assert.Single((await admin.GetFromJsonAsync<RoleAccess[]>("/api/v1/roles"))!, x => x.Name == "Viewer");
        var current = original;
        async Task Set(params string[] grants)
        {
            using var response = await admin.PutAsJsonAsync("/api/v1/roles/Viewer", new UpdateRoleRequest(grants, current.Version));
            current = await ReadAsync<RoleAccess>(response);
        }
        try
        {
            await Set([.. original.Permissions, Permissions.ExportCatalog, Permissions.ViewSensitiveReports, Permissions.ViewFinance, Permissions.ViewPurchasing]);
            using var viewer = await UserAsync(admin, "Viewer");
            Assert.Equal(123.45m, (await viewer.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{s.Product}"))!.PurchasePrice);
            await Set([.. original.Permissions, Permissions.ExportCatalog, Permissions.ViewFinance, Permissions.ViewPurchasing]);
            Assert.Null((await viewer.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{s.Product}"))!.PurchasePrice);
            var invoices = await viewer.GetFromJsonAsync<Page<InvoiceRow>>($"/api/v1/finance/invoices?partnerId={s.Customer}");
            Assert.Null(Assert.Single(invoices!.Items).CostAmount);
            var purchase = await viewer.GetFromJsonAsync<OrderDetail>($"/api/v1/orders/{s.Purchase}");
            Assert.Null(purchase!.Order.Total);
            using var workbook = await viewer.GetAsync("/api/v1/catalog/products/workbook");
            Assert.Equal(HttpStatusCode.OK, workbook.StatusCode);
            using var bytes = new MemoryStream(await workbook.Content.ReadAsByteArrayAsync());
            using var zip = new ZipArchive(bytes, ZipArchiveMode.Read);
            using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            var xml = XDocument.Load(sheet);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var row = Assert.Single(xml.Descendants(ns + "row"), r => r.Value.Contains(s.Code, StringComparison.Ordinal));
            Assert.Equal("", row.Elements(ns + "c").ElementAt(4).Value);
            await Set([.. original.Permissions, Permissions.UpdateCatalog]);
            var editable = await viewer.GetFromJsonAsync<ProductResponse>($"/api/v1/products/{s.Product}");
            Assert.Equal(123.45m, editable!.PurchasePrice);
            using var saved = await viewer.PutAsJsonAsync($"/api/v1/products/{s.Product}",
                new ProductInput(editable.Sku, editable.Name + " edited", editable.CategoryId, editable.UnitId,
                    editable.PurchasePrice!.Value, editable.SalePrice, editable.MinimumStock, editable.Description, editable.ImageUrl, editable.IsActive, editable.Version));
            Assert.Equal(123.45m, (await ReadAsync<ProductResponse>(saved)).PurchasePrice);
            using var denied = await viewer.GetAsync("/api/v1/finance/invoices");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.True(denied.Headers.Contains("X-Correlation-ID"));
        }
        finally { await Set(original.Permissions); }
    }

    private async Task<HttpClient> UserAsync(HttpClient admin, string role)
    {
        var email = $"security-{Guid.NewGuid():N}@minierp.example";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa7!";
        using var created = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Security boundary", password, role));
        await ReadAsync<UserAccount>(created);
        return await fixture.AuthenticatedClientAsync(email, password);
    }

    private static async Task<(Guid Product, Guid Customer, int Purchase, int Sale, int StockId, string Code)> SeedAsync(HttpClient admin)
    {
        var code = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        async Task<T> Post<T>(string path, object input)
        {
            using var response = await admin.PostAsJsonAsync("/api/v1/" + path, input);
            return await ReadAsync<T>(response);
        }
        var warehouse = await Post<LookupResponse>("warehouses", new LookupInput(code + "W", code, true, null));
        var category = await Post<LookupResponse>("categories", new LookupInput(code + "C", code, true, null));
        var unit = await Post<LookupResponse>("units", new LookupInput(code + "U", code, true, null));
        var product = await Post<ProductResponse>("products", new ProductInput(code, code, category.Id, unit.Id, 123.45m, 200, 0, null, null, true, null));
        var supplier = await Post<PartnerResponse>("suppliers", new PartnerInput(code, code, null, null, null, null, 0, 30, true, null));
        var customer = await Post<PartnerResponse>("customers", new PartnerInput(code, code, null, null, null, null, 0, 30, true, null));
        async Task<OrderDetail> Order(OrderKind kind, Guid partner, decimal quantity, decimal price)
        {
            var order = await Post<OrderDetail>("orders", new OrderInput(Guid.NewGuid(), kind, partner, warehouse.Id, null, [new(product.Id, quantity, price)]));
            order = await Post<OrderDetail>($"orders/{order.Order.Id}/submit", new OrderVersion(order.Order.Version));
            order = await Post<OrderDetail>($"orders/{order.Order.Id}/approve", new OrderVersion(order.Order.Version));
            return await Post<OrderDetail>($"orders/{order.Order.Id}/fulfill", new FulfillmentInput(Guid.NewGuid(), false, [new(product.Id, quantity)]));
        }
        var purchase = await Order(OrderKind.Purchase, supplier.Id, 4, 123.45m);
        var sale = await Order(OrderKind.Sales, customer.Id, 1, 200);
        return (product.Id, customer.Id, purchase.Order.Id, sale.Order.Id, Assert.Single(purchase.Fulfillments).StockDocumentId, code);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
