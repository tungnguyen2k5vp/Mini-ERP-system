using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Domain;

namespace MiniErp.Api;

internal sealed class ApiErrors(ILogger<ApiErrors> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception error, CancellationToken ct)
    {
        var (status, code, title, message) = error switch
        {
            ErpException e => (e.Kind switch
            {
                FailureKind.Invalid => 400,
                FailureKind.Missing => 404,
                _ => 409
            }, e.Code, "Không thể thực hiện yêu cầu.", e.Message),
            DbUpdateConcurrencyException => (409, "stale_version", "Xung đột dữ liệu.",
                "Dữ liệu đã thay đổi. Tải lại trước khi thử lại."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
                (409, "duplicate_record", "Dữ liệu đã tồn tại.", "Kiểm tra mã hoặc chứng từ liên quan."),
            SqlException { Number: 1205 or -2 } or DbUpdateException { InnerException: SqlException { Number: 1205 or -2 } } =>
                (409, "transaction_conflict", "Xung đột giao dịch.", "Tải lại dữ liệu và thử lại."),
            BadHttpRequestException bad => (bad.StatusCode, "invalid_request", "Yêu cầu không hợp lệ.",
                "Kiểm tra dữ liệu gửi lên."),
            _ => (500, "server_error", "Lỗi máy chủ.", "Không thể xử lý yêu cầu.")
        };

        if (status == 500)
        {
            // Avoid exception messages that may include credentials or request data.
            logger.LogError("Unhandled error {ErrorType} in {Method} {Path} with {CorrelationId}",
                error.GetType().Name, http.Request.Method, http.Request.Path, http.TraceIdentifier);
        }

        await Results.Problem(statusCode: status, title: title, detail: message,
            extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(http);
        return true;
    }
}
