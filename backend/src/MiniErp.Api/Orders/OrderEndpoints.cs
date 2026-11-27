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
            if (!(await auth.AuthorizeAsync(http.User,http,prefix+"View")).Succeeded && !(await auth.AuthorizeAsync(http.User,http,prefix+"Create")).Succeeded)
                return Results.Forbid();
            return Results.Ok(await service.OptionsAsync(kind,ct));
        });
        orders.MapGet("/",async (OrderKind kind,Guid? partnerId,int? page,int? pageSize,string? search,OrderStatus? status,string? sort,bool? descending,bool? onlyOverdue,HttpContext http,IAuthorizationService auth,IIdentityService identity,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,kind,"View");
            var (number,size) = ApiPaging.Validate(page,pageSize,search);
            if (status.HasValue && !Enum.IsDefined(status.Value) || sort is not (null or "number" or "createdAtUtc" or "total" or "status"))
                throw ErpException.Invalid("invalid_order_filter","Bộ lọc hoặc cột sắp xếp không hợp lệ.");
            if (kind == OrderKind.Purchase && sort == "total" &&
                !FinancialFields.PurchasePrices((await identity.GetProfileAsync(http.User.AccountId()!.Value,ct))?.Permissions ?? []))
                return Results.Forbid();
            return Results.Ok(await service.ListAsync(kind,partnerId,number,size,ct,search,status,sort,descending??true,onlyOverdue??false));
        });
        orders.MapGet("/{id:int}",async (int id,HttpContext http,IAuthorizationService auth,IIdentityService identity,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,await service.KindAsync(id,ct),"View");
            var detail = await service.DetailAsync(id,ct);
            return Results.Ok(detail with { CanApprove = CanApprove(detail,http,await GrantsAsync(http,identity),service) });
        });
        orders.MapPost("/",async (OrderInput input,HttpContext http,IAuthorizationService auth,IIdentityService identity,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,input.Kind,"Create");
            var result = await service.CreateAsync(input,http.Actor(),ct);
            result = result with { CanApprove = CanApprove(result,http,await GrantsAsync(http,identity),service) };
            return Results.Created($"/api/v1/orders/{result.Order.Id}",result);
        });
        orders.MapPut("/{id:int}",async (int id,OrderEditInput input,HttpContext http,IAuthorizationService auth,OrderService service,CancellationToken ct) =>
        {
            await RequireAsync(http,auth,await service.KindAsync(id,ct),"Create");
            return Results.Ok(await service.EditAsync(id,input,http.Actor(),ct));
        });
        foreach (var action in new[] { "submit","approve","cancel","reject","return-for-revision" })
        {
            orders.MapPost("/{id:int}/"+action,async (int id,OrderVersion input,HttpContext http,IAuthorizationService auth,IIdentityService identity,OrderService service,CancellationToken ct) =>
            {
                await RequireAsync(http,auth,await service.KindAsync(id,ct),action is "approve" or "reject" or "return-for-revision" ? "Approve" : action == "cancel" ? "Cancel" : "Create");
                var grants = await GrantsAsync(http,identity);
                var self = grants.Contains(Permissions.SelfApproveOrders);
                var above = grants.Contains(Permissions.ApproveOrdersAboveLimit);
                var detail = await service.TransitionAsync(id,action,input,http.Actor(),ct,self,above);
                return Results.Ok(detail with { CanApprove = CanApprove(detail,http,grants,service) });
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
    private static async Task<string[]> GrantsAsync(HttpContext http,IIdentityService identity) =>
        (await identity.GetProfileAsync(http.User.AccountId()!.Value,http.RequestAborted))?.Permissions ?? [];
    private static bool CanApprove(OrderDetail detail,HttpContext http,string[] grants,OrderService service)
    {
        var grant = detail.Order.Kind == OrderKind.Purchase ? Permissions.ApprovePurchasing : Permissions.ApproveSales;
        return grants.Contains(grant) && service.CanApprove(detail,http.Actor().UserId,
            grants.Contains(Permissions.SelfApproveOrders),grants.Contains(Permissions.ApproveOrdersAboveLimit));
    }
    private static async Task RequireAsync(HttpContext http,IAuthorizationService auth,OrderKind kind,string action)
    {
        if (!Enum.IsDefined(kind)) throw ErpException.Invalid("invalid_order_kind", "Loại đơn không hợp lệ.");
        if (!(await auth.AuthorizeAsync(http.User,http,(kind == OrderKind.Purchase ? "Purchasing." : "Sales.")+action)).Succeeded)
            throw new BadHttpRequestException("Forbidden",403);
    }
}
