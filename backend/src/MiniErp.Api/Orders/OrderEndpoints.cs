using Microsoft.AspNetCore.Authorization;
using MiniErp.Api.Auth;
using MiniErp.Application.Auth;
using MiniErp.Application.Orders;
using MiniErp.Domain;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Orders;
namespace MiniErp.Api.Orders;
internal static class OrderEndpoints
{
    public static void MapOrders(this RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/orders").WithTags("Orders").AddEndpointFilter<FinancialFields>();
        orders.MapGet("/options",async (OrderKind kind,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct)=>
        {
            if (!Enum.IsDefined(kind)) throw ErpException.Invalid("invalid_order_kind","Loại đơn không hợp lệ.");
            var prefix=kind==OrderKind.Purchase?"Purchasing.":"Sales.";
            if (!(await auth.AuthorizeAsync(http.User,null,prefix+"View")).Succeeded && !(await auth.AuthorizeAsync(http.User,null,prefix+"Create")).Succeeded)
                return Results.Forbid();
            return Results.Ok(await service.OptionsAsync(kind,ct));
        });
        orders.MapGet("/",async (OrderKind kind,Guid? partnerId,int? page,int? pageSize,string? search,OrderStatus? status,string? sort,bool? descending,HttpContext http,IAuthorizationService auth,IIdentityService identity,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,kind,"View");
            var (number,size) = ApiPaging.Validate(page,pageSize,search);
            if (status.HasValue && !Enum.IsDefined(status.Value) || sort is not (null or "number" or "createdAtUtc" or "total" or "status"))
                throw ErpException.Invalid("invalid_order_filter","Bộ lọc hoặc cột sắp xếp không hợp lệ.");
            if (kind == OrderKind.Purchase && sort == "total" &&
                !FinancialFields.PurchasePrices((await identity.GetProfileAsync(http.User.AccountId()!.Value,ct))?.Permissions ?? []))
                return Results.Forbid();
            return Results.Ok(await service.ListAsync(kind,partnerId,number,size,ct,search,status,sort,descending??true));
        });
        orders.MapGet("/{id:int}",async (int id,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,await service.KindAsync(id,ct),"View");
            return Results.Ok(await service.DetailAsync(id,ct));
        });
        orders.MapPost("/",async (OrderInput input,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,input.Kind,"Create");
            var result = await service.CreateAsync(input,http.Actor(),ct);
            return Results.Created($"/api/v1/orders/{result.Order.Id}",result);
        });
        foreach (var action in new[] { "submit","approve","cancel" })
        {
            orders.MapPost("/{id:int}/"+action,async (int id,OrderVersion input,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
            {
                await RequireAsync(http,auth,await service.KindAsync(id,ct),action == "approve" ? "Approve" : action == "cancel" ? "Cancel" : "Create");
                return Results.Ok(await service.TransitionAsync(id,action,input,http.Actor(),ct));
            });
        }
        orders.MapPost("/{id:int}/fulfill",async (int id,FulfillmentInput input,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,await service.KindAsync(id,ct),"View");
            return Results.Ok(await service.FulfillAsync(id,input,http.Actor(),ct));
        }).RequireAuthorization(Permissions.PostInventory);
        orders.MapPost("/{id:int}/fulfillments/{fulfillmentId:int}/reverse",async (int id,int fulfillmentId,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,await service.KindAsync(id,ct),"View");
            return Results.Ok(await service.ReverseAsync(id,fulfillmentId,http.Actor(),ct));
        }).RequireAuthorization(Permissions.PostInventory);
    }
    private static async Task RequireAsync(HttpContext http,IAuthorizationService auth,OrderKind kind,string action)
    {
        if (!Enum.IsDefined(kind)) throw ErpException.Invalid("invalid_order_kind", "Loại đơn không hợp lệ.");
        if (!(await auth.AuthorizeAsync(http.User,null,(kind == OrderKind.Purchase ? "Purchasing." : "Sales.")+action)).Succeeded)
            throw new BadHttpRequestException("Forbidden",403);
    }
}
