using Microsoft.AspNetCore.Authorization;
using MiniErp.Api.Auth;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Finance;
using MiniErp.Domain.Finance;
using MiniErp.Infrastructure.Finance;
using MiniErp.Application.Orders;
using MiniErp.Infrastructure.Orders;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Api.Finance;

internal static class FinanceEndpoints
{
    public static void MapFinance(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/finance").WithTags("Finance").RequireAuthorization(Permissions.ViewFinance);
        group.MapGet("/credit-exposure/{partnerId:guid}", async (Guid partnerId, ErpDbContext db, CancellationToken ct) =>
            Results.Ok(await CustomerCredit.ForPartnerAsync(db, partnerId, ct))).Produces<CreditExposure>();
        group.MapGet("/statement", async (Guid partnerId, DateTime? asOf, DateOnly? asOfDate, int? page, int? pageSize, FinanceReport report, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await report.StatementAsync(partnerId, asOf, asOfDate, number, size, ct));
        }).Produces<PartnerStatementPage>();
        group.MapGet("/invoices", async (FinanceDirection? direction, Guid? partnerId,long? invoiceId, int? page, int? pageSize, FinanceService service, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await service.InvoicesAsync(direction, partnerId, number, size, ct,invoiceId));
        }).AddEndpointFilter<FinancialFields>().Produces<Page<InvoiceRow>>();
        group.MapGet("/options",async(FinanceDirection direction,FinanceService service,CancellationToken ct)=>
            Results.Ok(await service.OptionsAsync(direction,ct))).Produces<FinanceOptions>();
        group.MapGet("/payments", async (FinanceDirection? direction,Guid? partnerId,int? page, int? pageSize, FinanceService service, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await service.PaymentsAsync(direction,partnerId,number, size, ct));
        }).Produces<Page<PaymentRow>>();
        group.MapPost("/payments", async (PaymentInput input, HttpContext http, FinanceService service, CancellationToken ct) =>
            Results.Ok(await service.PayAsync(input, http.Actor(), ct))).RequireAuthorization(Permissions.PostFinance).Produces<PaymentRow>().ProducesProblem(409);
        group.MapPost("/payments/{id:long}/reverse", async (long id, PaymentReversalInput input, HttpContext http,
            FinanceService service, CancellationToken ct) =>
            Results.Ok(await service.ReversePaymentAsync(id, input, http.Actor(), ct)))
            .RequireAuthorization(Permissions.PostFinance, Permissions.ReverseFinance).Produces<PaymentRow>().ProducesProblem(409);
        group.MapGet("/aging", async (DateTime? asOf,DateOnly? asOfDate, FinanceDirection? direction, FinanceReport report, CancellationToken ct) =>
            Results.Ok(await report.AgingAsync(asOf, direction, ct,asOfDate))).Produces<AgingRow[]>();
        group.MapGet("/reconciliation", async (DateTime? asOf,DateOnly? asOfDate, FinanceReport report, CancellationToken ct) =>
            Results.Ok(await report.ReconciliationAsync(asOf, ct,asOfDate))).Produces<ReconciliationRow[]>();
        group.MapGet("/reconciliation/exceptions",async(DateTime? asOf,DateOnly? asOfDate,int? orderId,FinanceDirection? direction,int? page,int? pageSize,HttpContext http,IAuthorizationService authorization,FinanceReport report,CancellationToken ct)=>
        {
            var(number,size)=ApiPaging.Validate(page,pageSize);
            var sensitive=(await authorization.AuthorizeAsync(http.User,http,Permissions.ViewSensitiveReports)).Succeeded;
            return Results.Ok(await report.ExceptionsAsync(asOf,asOfDate,number,size,ct,orderId,direction,sensitive));
        }).Produces<ReconciliationExceptionPage>();
    }
}
