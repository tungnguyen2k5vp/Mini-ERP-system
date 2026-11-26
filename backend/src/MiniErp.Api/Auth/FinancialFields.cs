using Microsoft.AspNetCore.Http.HttpResults;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Catalog;
using MiniErp.Application.Finance;
using MiniErp.Application.Inventory;
using MiniErp.Application.Orders;
using MiniErp.Domain.Orders;

namespace MiniErp.Api.Auth;

// Response projection only: historical entity values and posting calculations stay unchanged.
internal sealed class FinancialFields(IIdentityService identity) : IEndpointFilter
{
    public static bool PurchasePrices(IReadOnlyCollection<string> grants) => grants.Contains(Permissions.ViewSensitiveReports)
        || grants.Contains(Permissions.CreatePurchasing) || grants.Contains(Permissions.ApprovePurchasing);
    public static bool CatalogPrices(IReadOnlyCollection<string> grants) => PurchasePrices(grants)
        || grants.Contains(Permissions.CreateCatalog) || grants.Contains(Permissions.UpdateCatalog);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);
        if (result is not IValueHttpResult response || response.Value is null) return result;
        var profile = await identity.GetProfileAsync(context.HttpContext.User.AccountId()!.Value, context.HttpContext.RequestAborted);
        var grants = profile?.Permissions ?? [];
        var sensitive = grants.Contains(Permissions.ViewSensitiveReports);
        var purchase = PurchasePrices(grants);
        var catalog = CatalogPrices(grants);
        object Project(object value) => value switch
        {
            ProductResponse p => p with { PurchasePrice = catalog ? p.PurchasePrice : null },
            Page<ProductResponse> p => p with { Items = p.Items.Select(x => (ProductResponse)Project(x)).ToArray() },
            StockBalanceRow b => b with { AverageCost = sensitive ? b.AverageCost : null },
            Page<StockBalanceRow> p => p with { Items = p.Items.Select(x => (StockBalanceRow)Project(x)).ToArray() },
            StockMovementRow m => m with { UnitCost = sensitive ? m.UnitCost : null },
            Page<StockMovementRow> p => p with { Items = p.Items.Select(x => (StockMovementRow)Project(x)).ToArray() },
            StockDocumentResponse d => d with { Lines = d.Lines.Select(x => x with { UnitCost = sensitive ? x.UnitCost : null }).ToArray() },
            StockProductOption[] p => p.Select(x => x with { PurchasePrice = catalog ? x.PurchasePrice : null }).ToArray(),
            OrderOptions o => o with { Products = o.Products.Select(x => x with { PurchasePrice = purchase ? x.PurchasePrice : null }).ToArray() },
            OrderRow o => o with { Total = o.Kind == OrderKind.Sales || purchase ? o.Total : null },
            Page<OrderRow> p => p with { Items = p.Items.Select(x => (OrderRow)Project(x)).ToArray() },
            OrderDetail d => d.Order.Kind == OrderKind.Sales || purchase ? d : d with
            {
                Order = (OrderRow)Project(d.Order),
                Lines = d.Lines.Select(x => x with { UnitPrice = null }).ToArray(),
                Fulfillments = d.Fulfillments.Select(x => x with { Amount = null }).ToArray()
            },
            Page<InvoiceRow> p => p with { Items = p.Items.Select(x => x with { CostAmount = sensitive ? x.CostAmount : null }).ToArray() },
            _ => value
        };
        var projected = Project(response.Value);
        if (ReferenceEquals(projected, response.Value)) return result;
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (result is Created<ProductResponse> product) context.HttpContext.Response.Headers.Location = product.Location;
        if (result is Created<OrderDetail> order) context.HttpContext.Response.Headers.Location = order.Location;
        if (result is Created<StockDocumentResponse> stock) context.HttpContext.Response.Headers.Location = stock.Location;
        return Results.Json(projected, statusCode: (result as IStatusCodeHttpResult)?.StatusCode);
    }
}
