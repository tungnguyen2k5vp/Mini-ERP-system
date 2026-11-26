using MiniErp.Api.Auth;
using MiniErp.Application;
using MiniErp.Application.Auth;
using MiniErp.Application.Reporting;
using MiniErp.Domain;
using MiniErp.Infrastructure.Reporting;

namespace MiniErp.Api.Reporting;

internal static class OperationsEndpoints
{
    public static void MapOperationsReports(this RouteGroupBuilder api)
    {
        api.MapGet("/reports/dashboard", async (DateOnly? from,DateOnly? to,HttpContext http,IIdentityService identity,OperationsReport report,CancellationToken ct) =>
            Results.Ok(await report.DashboardAsync(from,to,await Grants(http,identity,ct),ct)))
            .RequireAuthorization(Permissions.ViewReports).Produces<DashboardResponse>();
        api.MapGet("/reports/stock",async(DateOnly? from,DateOnly? to,Guid? warehouseId,string? filter,int? page,int? pageSize,OperationsReport report,CancellationToken ct)=>
        {
            var(number,size)=ApiPaging.Validate(page,pageSize);
            return Results.Ok(await report.StockAsync(from,to,warehouseId,filter,number,size,ct));
        }).RequireAuthorization(Permissions.ViewReports,Permissions.ViewInventory).Produces<Page<StockReportRow>>();
        api.MapGet("/search",async(string? q,HttpContext http,IIdentityService identity,OperationsReport report,CancellationToken ct)=>
            Results.Ok(await report.SearchAsync(q,await Grants(http,identity,ct),ct))).RequireAuthorization().Produces<SearchHit[]>();
        api.MapGet("/audit",async(DateOnly? from,DateOnly? to,string? search,int? page,int? pageSize,OperationsReport report,CancellationToken ct)=>
        {
            var(number,size)=ApiPaging.Validate(page,pageSize,search);
            return Results.Ok(await report.AuditAsync(from,to,search,number,size,ct));
        }).RequireAuthorization(Permissions.ViewAudit).Produces<Page<AuditRow>>();
        api.MapGet("/catalog/products/workbook",async(ProductWorkbook workbook,CancellationToken ct)=>
            Results.File(await workbook.ExportAsync(ct),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","MiniErp-products.xlsx"))
            .RequireAuthorization(Permissions.ViewCatalog,Permissions.ExportCatalog);
        api.MapPost("/catalog/products/workbook",async(HttpContext http,ProductWorkbook workbook,CancellationToken ct)=>
        {
            const long limit=2*1024*1024;
            var bodyLimit=http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if(bodyLimit is {IsReadOnly:false}) bodyLimit.MaxRequestBodySize=limit+65536;
            if (!http.Request.HasFormContentType || http.Request.ContentLength is > limit + 65536)
                throw ErpException.Invalid("workbook_upload","Chọn tệp Excel tối đa 2 MB.");
            IFormCollection form;
            try { form=await http.Request.ReadFormAsync(new Microsoft.AspNetCore.Http.Features.FormOptions {MultipartBodyLengthLimit=limit},ct); }
            catch(InvalidDataException) { throw ErpException.Invalid("workbook_upload","Tệp tải lên không hợp lệ hoặc vượt 2 MB."); }
            var file=form.Files.GetFile("file");
            if (file is null || form.Files.Count != 1 || file.Length is <=0 or >limit || !string.Equals(Path.GetExtension(file.FileName),".xlsx",StringComparison.OrdinalIgnoreCase))
                throw ErpException.Invalid("workbook_upload","Chọn một tệp .xlsx tối đa 2 MB.");
            await using var stream=file.OpenReadStream();
            return Results.Ok(new { imported=await workbook.ImportAsync(stream,http.Actor(),ct) });
        }).RequireAuthorization(Permissions.ViewCatalog,Permissions.CreateCatalog)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2*1024*1024+65536));
    }
    private static async Task<HashSet<string>> Grants(HttpContext http,IIdentityService identity,CancellationToken ct)
    {
        var profile=await identity.GetProfileAsync(http.User.AccountId()!.Value,ct);
        return (profile?.Permissions ?? []).ToHashSet(StringComparer.Ordinal);
    }
}
