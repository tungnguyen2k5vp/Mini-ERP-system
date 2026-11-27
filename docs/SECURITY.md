# Security

Hạn mức khách hàng đã tồn tại không thể bị tăng, giảm hoặc xóa về 0 chỉ bằng `Catalog.Update`: cần thêm `Finance.ChangeCreditLimit`, mặc định chỉ quản trị viên có và cấp được riêng. Kiểm tra so với giá trị đang lưu cùng rowversion xảy ra trước mọi thay đổi liên hệ. Tạo mới vẫn dùng `Catalog.Create` để đặt chính sách ban đầu; nhật ký ghi riêng giá trị ban đầu và mọi thay đổi sau đó, kèm tác giả và hạn mức trước/sau.

`Finance.Reverse` và `Sales.OverrideCreditLimit` là quyền độc lập, mặc định chỉ quản trị viên có và có thể cấp riêng theo vai trò. Đảo thanh toán vẫn cần `Finance.View` và `Finance.Post`; vượt hạn mức vẫn cần `Sales.Approve` và các điều kiện duyệt khác. Hai hành động phải ghi lý do, người thực hiện và nguồn liên kết; không sửa chứng từ tiền gốc. Tín dụng hiện tại khác sao kê theo ngày chốt. API không cho lùi thời điểm thu chi/đảo qua sự kiện công nợ liên quan hoặc đảo làm tiền hoàn mất bảo đảm.

Server-side field permissions cover purchase price, COGS and margin across JSON responses, order sorting and Excel cells. Inspection of a sales source-cost discrepancy requires the same sensitive-cost grant as the numeric values. A restricted row is excluded before computing the exception count and page. [Endpoint and field matrix](API_PERMISSION_MATRIX.md)

The commercial endpoints resolve the actual order kind in SQL before checking the module grant. Fulfillment and reversal also require Inventory.Post. Finance reads and postings have separate grants. Dashboard, search and work queues filter by current permissions. SQL integration cases exercise denial for anonymous, Viewer and management users.

Order approval checks the creator, latest editor and submitter against the current approver inside the order transaction. Self-approval and approval at or above the configured VND threshold each require a separate grant. Warehouse edits preserve hidden costs on the server and reject explicit valuation changes by restricted users. [Approval and field permissions](API_PERMISSION_MATRIX.md)

Workbook imports constrain the HTTP body, the expanded ZIP/XML and the row count. Parsed identifiers and decimal values use ordinary catalog validation and a transaction. Operational logs include route templates, response status, duration and correlation ID. Authenticated API responses use no-store. The access token, cookie value, form payloads and connection strings are excluded from request logs.

Identity hashes passwords with its configured password hasher and temporarily locks an account after five failed attempts. Login is rate limited. The API uses a configured Base64 signing key of at least 32 bytes. Refresh credentials are random and stored as SHA-256 hashes. SQL rowversion makes a rotation single-use. Session validation rejects disabled, expired, rotated-out and revoked credentials as appropriate. The refresh cookie has HttpOnly and SameSite=Strict, and Secure in production. Origin and a custom header protect cookie operations.

Administrative role and lock changes update the security stamp. User and role administration remain restricted to Administrator. Other role grants can be changed independently. Permissions are resolved from SQL Server on protected routes. Administrative accounts cannot be locked or reassigned through the ordinary user editor.

Development uses a local generated key and password under the ignored .work directory. Production uses external secrets and a separate provisioning command. The UI holds access tokens in memory and clears cached server data when the user changes. The confirmation dialog is separate from server-side authorization.

Responses expose a correlation ID for investigation. Login and changes to account access write audit records with bounded change details. No password, JWT, refresh credential or connection string belongs in audit records.

Current verification and unresolved checks are listed in [test report](TEST_REPORT.md) and [known issues](KNOWN_ISSUES.md).
