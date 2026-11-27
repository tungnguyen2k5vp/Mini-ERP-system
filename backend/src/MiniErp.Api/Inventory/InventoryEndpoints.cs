using MiniErp.Api.Auth;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Inventory;
using MiniErp.Domain;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Inventory;

namespace MiniErp.Api.Inventory;

internal static class InventoryEndpoints
{
    public static void MapInventory(this RouteGroupBuilder api)
    {
        var inventory = api.MapGroup("/inventory").WithTags("Inventory").RequireAuthorization(Permissions.ViewInventory).AddEndpointFilter<FinancialFields>();
        inventory.MapGet("/balances", async (Guid? warehouseId, Guid? productId, string? search, bool? lowOnly, int? page, int? pageSize,
            InventoryReport report, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize, search);
            return Results.Ok(await report.GetPageAsync(warehouseId, productId, search, lowOnly ?? false, number, size, ct));
        }).Produces<Page<StockBalanceRow>>().WithName("GetInventoryBalances");
        inventory.MapGet("/history", async (Guid? warehouseId, Guid? productId, int? page, int? pageSize, StockService stock, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await stock.HistoryAsync(warehouseId, productId, number, size, ct));
        }).Produces<Page<StockMovementRow>>().WithName("GetInventoryHistory");
        inventory.MapGet("/product-options", async (StockService stock, CancellationToken ct) =>
            Results.Ok(await stock.ProductOptionsAsync(ct))).Produces<StockProductOption[]>();

        var docs = inventory.MapGroup("/documents");
        docs.MapGet("/", async (StockKind? kind, StockStatus? status, Guid? warehouseId, int? page, int? pageSize,
            StockService stock, CancellationToken ct) =>
        {
            if ((kind.HasValue && !Enum.IsDefined(kind.Value)) || (status.HasValue && !Enum.IsDefined(status.Value)))
            {
                throw ErpException.Invalid("invalid_stock_filter", "Loại phiếu hoặc trạng thái không hợp lệ.");
            }
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await stock.DocumentsAsync(kind, status, warehouseId, number, size, ct));
        }).Produces<Page<StockDocumentRow>>().WithName("ListStockDocuments");
        docs.MapGet("/{id:int}", async (int id, StockService stock, CancellationToken ct) =>
            Results.Ok(await stock.DetailAsync(id, ct))).Produces<StockDocumentResponse>().WithName("GetStockDocument");
        docs.MapPost("/", async (StockDocumentInput input, HttpContext http, StockService stock, CancellationToken ct) =>
        {
            var result = await stock.CreateAsync(input, http.Actor(), ct);
            return Results.Created($"/api/v1/inventory/documents/{result.Document.Id}", result);
        }).RequireAuthorization(Permissions.PostInventory).Produces<StockDocumentResponse>(201).WithName("CreateStockDocument");
        docs.MapPut("/{id:int}", async (int id, StockDocumentEditInput input, HttpContext http,
            IIdentityService identity, StockService stock, CancellationToken ct) =>
        {
            var profile = await identity.GetProfileAsync(http.User.AccountId()!.Value, ct);
            var sensitive = profile?.Permissions.Contains(Permissions.ViewSensitiveReports) == true;
            return Results.Ok(await stock.EditAsync(id, input, http.Actor(), sensitive, ct));
        }).RequireAuthorization(Permissions.PostInventory).Produces<StockDocumentResponse>().ProducesProblem(409);
        docs.MapPost("/{id:int}/post", async (int id, StockPostInput? input, HttpContext http, StockService stock, CancellationToken ct) =>
        {
            if (input is not null && input.Version is not { Length: 8 })
                throw ErpException.Invalid("invalid_version", "Phiên bản không hợp lệ.");
            return Results.Ok(await stock.PostAsync(id, http.Actor(), ct, version: input?.Version));
        }).RequireAuthorization(Permissions.PostInventory)
            .Produces<StockDocumentResponse>().ProducesProblem(409).WithName("PostStockDocument");
        docs.MapPost("/{id:int}/cancel", async (int id, StockCancelInput input, HttpContext http, StockService stock, CancellationToken ct) =>
            Results.Ok(await stock.CancelAsync(id, input, http.Actor(), ct))).RequireAuthorization(Permissions.PostInventory)
            .Produces<StockDocumentResponse>().ProducesProblem(409).WithName("CancelStockDocument");
        docs.MapPost("/{id:int}/reverse", async (int id, HttpContext http, StockService stock, CancellationToken ct) =>
            Results.Ok(await stock.ReverseAsync(id, http.Actor(), ct))).RequireAuthorization(Permissions.PostInventory)
            .Produces<StockDocumentResponse>().ProducesProblem(409).WithName("ReverseStockDocument");
    }
}
