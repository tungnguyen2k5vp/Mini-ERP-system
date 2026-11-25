using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Auth;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Infrastructure.Catalog;

namespace MiniErp.Api.Catalog;

internal static class CatalogEndpoints
{
    public static void MapCatalog(this RouteGroupBuilder api)
    {
        var products = api.MapGroup("/products").WithTags("Products");
        products.MapGet("/", async ([AsParameters] Listing query, Guid? categoryId, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListProductsAsync(query.ToFilter("sku", "salePrice", "minimumStock"), categoryId, ct)))
            .RequireAuthorization(Permissions.ViewCatalog).Produces<Page<ProductResponse>>().WithName("ListProducts");
        products.MapGet("/{id:guid}", async (Guid id, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetProductAsync(id, ct))).RequireAuthorization(Permissions.ViewCatalog)
            .Produces<ProductResponse>().WithName("GetProduct");
        products.MapPost("/", async (ProductInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
        {
            var product = await catalog.SaveProductAsync(null, input, http.Actor(), ct);
            return Results.Created($"/api/v1/products/{product.Id}", product);
        }).RequireAuthorization(Permissions.CreateCatalog).Produces<ProductResponse>(201).WithName("CreateProduct");
        products.MapPut("/{id:guid}", async (Guid id, ProductInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.SaveProductAsync(id, input, http.Actor(), ct)))
            .RequireAuthorization(Permissions.UpdateCatalog).Produces<ProductResponse>().ProducesProblem(409).WithName("UpdateProduct");

        MapLookups(api, "categories", LookupKind.Category);
        MapLookups(api, "units", LookupKind.Unit);
        MapLookups(api, "warehouses", LookupKind.Warehouse);
        MapLookups(api, "payment-methods", LookupKind.PaymentMethod);
        MapPartners(api, "customers", PartnerKind.Customer);
        MapPartners(api, "suppliers", PartnerKind.Supplier);
    }

    private static void MapLookups(RouteGroupBuilder api, string resource, LookupKind kind)
    {
        var group = api.MapGroup("/" + resource).WithTags(kind.ToString());
        group.MapGet("/", async ([AsParameters] Listing query, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListLookupsAsync(kind, query.ToFilter("code"), ct)))
            .RequireAuthorization(Permissions.ViewCatalog).Produces<Page<LookupResponse>>();
        group.MapGet("/options", async (Guid? includedId, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.LookupOptionsAsync(kind, includedId, ct)))
            .RequireAuthorization(Permissions.ViewCatalog).Produces<LookupOption[]>();
        group.MapGet("/{id:guid}", async (Guid id, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetLookupAsync(kind, id, ct))).RequireAuthorization(Permissions.ViewCatalog).Produces<LookupResponse>();
        group.MapPost("/", async (LookupInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
        {
            var row = await catalog.SaveLookupAsync(kind, null, input, http.Actor(), ct);
            return Results.Created($"/api/v1/{resource}/{row.Id}", row);
        }).RequireAuthorization(Permissions.CreateCatalog).Produces<LookupResponse>(201);
        group.MapPut("/{id:guid}", async (Guid id, LookupInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.SaveLookupAsync(kind, id, input, http.Actor(), ct)))
            .RequireAuthorization(Permissions.UpdateCatalog).Produces<LookupResponse>().ProducesProblem(409);
    }

    private static void MapPartners(RouteGroupBuilder api, string resource, PartnerKind kind)
    {
        var group = api.MapGroup("/" + resource).WithTags(kind.ToString());
        group.MapGet("/", async ([AsParameters] Listing query, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.ListPartnersAsync(kind, query.ToFilter("code"), ct)))
            .RequireAuthorization(Permissions.ViewCatalog).Produces<Page<PartnerResponse>>();
        group.MapGet("/{id:guid}", async (Guid id, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.GetPartnerAsync(kind, id, ct))).RequireAuthorization(Permissions.ViewCatalog).Produces<PartnerResponse>();
        group.MapPost("/", async (PartnerInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
        {
            var row = await catalog.SavePartnerAsync(kind, null, input, http.Actor(), ct);
            return Results.Created($"/api/v1/{resource}/{row.Id}", row);
        }).RequireAuthorization(Permissions.CreateCatalog).Produces<PartnerResponse>(201);
        group.MapPut("/{id:guid}", async (Guid id, PartnerInput input, HttpContext http, CatalogService catalog, CancellationToken ct) =>
            Results.Ok(await catalog.SavePartnerAsync(kind, id, input, http.Actor(), ct)))
            .RequireAuthorization(Permissions.UpdateCatalog).Produces<PartnerResponse>().ProducesProblem(409);
    }

    internal sealed record Listing(
        [FromQuery] int? Page, [FromQuery] int? PageSize, [FromQuery] string? Search, [FromQuery] bool? Active,
        [FromQuery] string? Sort, [FromQuery] bool? Descending)
    {
        public CatalogPageQuery ToFilter(params string[] validSorts)
        {
            var (page, size) = ApiPaging.Validate(Page, PageSize, Search);
            var sort = Sort ?? "name";
            if (sort != "name" && !validSorts.Contains(sort))
            {
                throw ErpException.Invalid("invalid_sort", "Cột sắp xếp không được hỗ trợ.");
            }
            return new CatalogPageQuery(page, size, Search?.Trim(), Active, sort, Descending ?? false);
        }
    }
}
