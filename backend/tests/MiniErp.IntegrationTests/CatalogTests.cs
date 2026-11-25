using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;

namespace MiniErp.IntegrationTests;

[Collection("SqlServer")]
public sealed class CatalogTests(ErpFixture fixture)
{
    [SqlServerFact]
    public async Task Product_enforces_typed_references_unique_sku_and_version()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var code = "E2E-" + Guid.NewGuid().ToString("N")[..10];
        var category = await CreateLookupAsync(admin, "categories", code);
        var unit = await CreateLookupAsync(admin, "units", code);
        var input = new ProductInput(code.ToLowerInvariant(), "Bút kiểm thử", category.Id, unit.Id, 10, 15.25m, 5, null, null, true, null);
        using var invalid = await admin.PostAsJsonAsync("/api/v1/products", input with { CategoryId = unit.Id });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var created = await admin.PostAsJsonAsync("/api/v1/products", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await ReadAsync<ProductResponse>(created);
        Assert.Equal(code.ToUpperInvariant(), product.Sku);
        Assert.Equal(category.Name, product.CategoryName);
        Assert.Equal(TimeSpan.Zero, product.CreatedAt.Offset);
        Assert.Equal(8, product.Version.Length);

        using var duplicate = await admin.PostAsJsonAsync("/api/v1/products", input);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var updated = await admin.PutAsJsonAsync($"/api/v1/products/{product.Id}",
            input with { Version = product.Version, IsActive = false, SalePrice = 19.75m });
        var latest = await ReadAsync<ProductResponse>(updated);
        Assert.False(latest.IsActive);
        Assert.Equal(19.75m, latest.SalePrice);

        using var stale = await admin.PutAsJsonAsync($"/api/v1/products/{product.Id}", input with { Version = product.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var list = await admin.GetAsync($"/api/v1/products?active=false&search={code}&sort=sku&pageSize=1");
        var row = Assert.Single((await ReadAsync<Page<ProductResponse>>(list)).Items);
        Assert.Equal(product.Id, row.Id);
        Assert.False(row.IsActive);
    }

    [SqlServerFact]
    public async Task Partner_routes_scope_records_and_catalog_permissions_are_enforced()
    {
        using var admin = await fixture.AuthenticatedClientAsync();
        var code = "P-" + Guid.NewGuid().ToString("N")[..10];
        var partner = new PartnerInput(code, "Đối tác thử nghiệm", null, null, null, null, 1000m, 30, true, null);

        using var created = await admin.PostAsJsonAsync("/api/v1/customers", partner);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = await ReadAsync<PartnerResponse>(created);
        using var otherKind = await admin.GetAsync($"/api/v1/suppliers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherKind.StatusCode);

        var email = $"catalog-viewer-{Guid.NewGuid():N}@minierp.example";
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "Aa7!";
        using var account = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(email, "Người xem", password, "Viewer"));
        Assert.Equal(HttpStatusCode.Created, account.StatusCode);
        using var viewer = await fixture.AuthenticatedClientAsync(email, password);
        using var readable = await viewer.GetAsync($"/api/v1/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.OK, readable.StatusCode);
        using var denied = await viewer.PostAsJsonAsync("/api/v1/customers", partner with { Code = code + "X" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static async Task<LookupResponse> CreateLookupAsync(HttpClient admin, string resource, string code)
    {
        using var response = await admin.PostAsJsonAsync("/api/v1/" + resource, new LookupInput(code, "Mẫu " + code, true, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<LookupResponse>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
