using MiniErp.Api.Auth;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Finance;
using MiniErp.Domain.Finance;
using MiniErp.Infrastructure.Finance;

namespace MiniErp.Api.Finance;

internal static class FinanceEndpoints
{
    public static void MapFinance(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/finance").WithTags("Finance").RequireAuthorization(Permissions.ViewFinance);
        group.MapGet("/invoices", async (FinanceDirection? direction, Guid? partnerId,long? invoiceId, int? page, int? pageSize, FinanceService service, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await service.InvoicesAsync(direction, partnerId, number, size, ct,invoiceId));
        }).Produces<Page<InvoiceRow>>();
        group.MapGet("/options",async(FinanceDirection direction,FinanceService service,CancellationToken ct)=>
            Results.Ok(await service.OptionsAsync(direction,ct))).Produces<FinanceOptions>();
        group.MapGet("/payments", async (FinanceDirection? direction,Guid? partnerId,int? page, int? pageSize, FinanceService service, CancellationToken ct) =>
        {
            var (number, size) = ApiPaging.Validate(page, pageSize);
            return Results.Ok(await service.PaymentsAsync(direction,partnerId,number, size, ct));
        }).Produces<Page<PaymentRow>>();
        group.MapPost("/payments", async (PaymentInput input, HttpContext http, FinanceService service, CancellationToken ct) =>
            Results.Ok(await service.PayAsync(input, http.Actor(), ct))).RequireAuthorization(Permissions.PostFinance).Produces<PaymentRow>().ProducesProblem(409);
        group.MapGet("/aging", async (DateTime? asOf, FinanceDirection? direction, FinanceReport report, CancellationToken ct) =>
            Results.Ok(await report.AgingAsync(asOf, direction, ct))).Produces<AgingRow[]>();
        group.MapGet("/reconciliation", async (DateTime? asOf, FinanceReport report, CancellationToken ct) =>
            Results.Ok(await report.ReconciliationAsync(asOf, ct))).Produces<ReconciliationRow[]>();
    }
}
