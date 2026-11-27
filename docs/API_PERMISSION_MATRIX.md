# API permissions

## Routes

Routes below share the `/api/v1` prefix. For order routes, `Kind` means the permission prefix `Purchasing` or `Sales`. The server resolves the kind from the stored order for routes with an ID.

| Method and resource | Access |
| --- | --- |
| POST `/auth/login`, `/auth/refresh`, `/auth/logout` | First-party origin and session header. Login and session operations are rate limited. |
| GET `/auth/me`, POST `/auth/change-password` | Active session belonging to the user. Password changes also require the first-party request check. |
| GET / POST `/users`, GET / PUT `/users/{id}` | `System.Users` |
| GET `/roles`, PUT `/roles/{name}`, GET `/permissions` | `System.Roles` |
| Products, categories, units, warehouses, payment-methods, customers and suppliers | GET list and detail requires `Catalog.View`. GET options for the four lookup resources requires the same grant. POST requires `Catalog.Create`. PUT by ID requires `Catalog.Update`. |
| GET / POST `/catalog/products/workbook` | `Catalog.View` plus `Catalog.Export` for download or `Catalog.Create` for upload |
| GET `/inventory/balances`, `/inventory/history`, `/inventory/product-options`, `/inventory/documents`, `/inventory/documents/{id}` | `Inventory.View` |
| POST `/inventory/documents`, PUT `/inventory/documents/{id}` and POST `/inventory/documents/{id}/{post,cancel,reverse}` | `Inventory.View` and `Inventory.Post` |
| GET `/orders/options`, GET `/orders` and `/orders/{id}` | Options allow `Kind.View` or `Kind.Create`. List/detail require `Kind.View`. Sorting by purchase total also requires purchase-price visibility. |
| POST `/orders`, PUT `/orders/{id}`, POST `/orders/{id}/{submit,approve,cancel,reject,return-for-revision}` | Create/edit/submit require `Kind.Create`; approve/reject/return-for-revision require `Kind.Approve`; cancel requires `Kind.Cancel`. Approval additionally enforces maker separation and the optional inclusive amount threshold. |
| POST `/orders/{id}/fulfill`, `/orders/{id}/fulfillments/{fulfillmentId}/reverse` | `Kind.View` and `Inventory.Post`. The fulfillment must belong to the given order. |
| GET `/finance/invoices`, `/finance/options`, `/finance/payments`, `/finance/aging`, `/finance/reconciliation`, `/finance/reconciliation/exceptions` | `Finance.View`. Cost discrepancies require `Reports.Sensitive` for inclusion in either the rows or the count. |
| POST `/finance/payments` | `Finance.View` and `Finance.Post` |
| GET `/reports/dashboard`, `/reports/stock`, `/reports/stock/reconciliation` | `Reports.View`. Both stock endpoints also require `Inventory.View`. Dashboard subsets follow current operational grants. |
| GET `/search`, `/audit` | Search requires an active session and filters each result type by grant. Audit requires `System.Audit`. |

Outside the API prefix, `/health/live` and `/health/ready` expose health status. The application maps its OpenAPI document only in Development.

## Field rules

Field projection reuses the commercial and valuation grants. Endpoint access and field access are separate: a role may read quantities and references without receiving historical financial fields. Hidden response fields are JSON `null`, never zero. The browser shows `—`, including printed documents. Entity snapshots and posting calculations are unchanged.

| Surface | Endpoint access | Financial fields |
| --- | --- | --- |
| Catalog product list/detail | `Catalog.View` | Purchase price requires `Reports.Sensitive`, `Purchasing.Create`, `Purchasing.Approve`, `Catalog.Create` or `Catalog.Update`. Sale list price remains catalog data. |
| Catalog create/update response | `Catalog.Create` / `Catalog.Update` | These grants include catalog price entry and visibility. The editor refuses to load a redacted price into an update form. Writes retain the existing validation and concurrency checks. |
| Product workbook export | `Catalog.View` + `Catalog.Export` | Same purchase-price rule as catalog JSON. Unauthorized purchase-price cells are blank. The workbook preserves headers; blank prices are not fabricated zero values. |
| Product workbook import | `Catalog.View` + `Catalog.Create` | Price entry is authorized by catalog creation. Import validation is unchanged. |
| Inventory balances | `Inventory.View` | `AverageCost` requires `Reports.Sensitive`. Quantities, thresholds and availability remain visible. |
| Inventory movement history | `Inventory.View` | `UnitCost` requires `Reports.Sensitive`. Signed quantities remain visible. |
| Stock document read/create/edit/post/cancel/reverse responses | `Inventory.View`; writes also `Inventory.Post` | Line `UnitCost` requires `Reports.Sensitive` in responses. Posting remains available to warehouse staff. Creation retains explicit receipt-cost input. On edit, explicit costs require Reports.Sensitive; omitted/null retained-product costs are preserved and new restricted receipt products use their own server purchase-price snapshot. |
| Inventory product options | `Inventory.View` | Catalog purchase-price rule. A hidden suggestion remains null. On receipt creation the existing cost-entry rule applies; on restricted receipt edits the server supplies new-product cost. |
| Purchase order list/detail/print and mutation responses | Existing kind-specific endpoint grants | `Total`, line `UnitPrice` and fulfillment `Amount` require `Reports.Sensitive`, `Purchasing.Create` or `Purchasing.Approve`. Plain `Purchasing.View` permits logistics quantities and status, including warehouse fulfillment. |
| Order product options | Kind-specific View or Create grant | Purchase-price suggestions require `Reports.Sensitive`, `Purchasing.Create` or `Purchasing.Approve`. |
| Sales order list/detail/print | `Sales.View` (creation retains `Sales.Create`) | Contract sale prices, totals and fulfillment amounts remain visible. Warehouse staff already hold `Sales.View`; this correction does not revoke that commercial read grant. |
| Finance invoices | `Finance.View` | Settlement and invoice amounts remain available. `CostAmount` requires `Reports.Sensitive`. |
| Dashboard profit/cost/margin | `Reports.View` | Existing `Reports.Sensitive` and `Sales.View` check remains in force. |

The API projects response records after authorization; it does not zero or modify historical entities. Product and order creation retain HTTP 201 and their Location header. Projected responses use `Cache-Control: no-store`. Generic errors retain their existing Problem Details response.

Purchase total sorting follows the same commercial permission as the returned total. The API returns 403 for `kind=1&sort=total` without that grant, and the corresponding table header does not offer sorting. This prevents disclosing the ranking of hidden purchase amounts.

Permissions are read from the current profile on every request. Existing bearer tokens therefore lose field visibility immediately after a role grant is removed. Sessions additionally check revocation, disabled/locked user state and security stamp at authentication.

`SecurityBoundaryTests` uses ordinary user provisioning/login APIs and the shared SQL Server collection. It covers the built-in role matrix, stock quantities without valuation, warehouse purchase contracts, sales contract visibility, invoice COGS, workbook price cells, catalog editing and grant removal on an existing token. Role changes restore the original grants in `finally`. Coordinating checks must run against a separate MiniErp test database; results are reported in the task verification, not assumed here.

## Approval overrides

`Orders.SelfApprove` overrides maker separation for the creator, latest editor, latest submitter and unknown legacy creator. `Orders.ApproveAboveLimit` is additionally required when the rounded order total is greater than or equal to configured `Orders__ApprovalThresholdVnd`. Neither override replaces the module Approve grant. An unset threshold disables only the amount check. Default Manager grants contain neither override; administrators retain their permission bypass. Actor IDs come from audit events, without inventing attribution for migrated records.

## Milestone 3: quyền tín dụng và đảo thu chi

| Tác vụ | Quyền bắt buộc |
| --- | --- |
| Sao kê `/finance/statement`, tín dụng khách hàng `/finance/credit-exposure/{partnerId}` | `Finance.View` |
| Tín dụng của đơn `/orders/{id}/credit-exposure` | `Sales.Approve` hoặc cả `Finance.View` và `Sales.View` |
| Duyệt đơn bán vượt hạn mức dương | `Sales.Approve` cùng các điều kiện duyệt hiện hành, thêm `Sales.OverrideCreditLimit` và lý do |
| Đảo phiếu `/finance/payments/{id}/reverse` | Đồng thời `Finance.View`, `Finance.Post`, `Finance.Reverse` |
| Đổi hạn mức của khách hàng đã tồn tại, kể cả giảm hoặc đặt 0 | Đồng thời `Catalog.Update` và `Finance.ChangeCreditLimit`; giữ nguyên hạn mức để sửa liên hệ chỉ cần `Catalog.Update` |

`Finance.Reverse` và `Sales.OverrideCreditLimit` được cấp độc lập theo vai trò; mặc định chỉ quản trị viên có hai quyền này. Kế toán có quyền thu chi không mặc nhiên được đảo, người duyệt đơn không mặc nhiên được vượt hạn mức. Hạn mức 0 là không giới hạn. Thay đổi quyền vẫn được kiểm tra ở máy chủ với phiên hiện tại; giao diện xác nhận không thay thế chính sách này.

`Finance.ChangeCreditLimit` cũng được cấp độc lập, mặc định chỉ quản trị viên có. `Catalog.Create` vẫn cho phép đặt hạn mức ban đầu khi tạo khách hàng, bao gồm 0; nhật ký ghi riêng chính sách ban đầu. Mọi thay đổi hạn mức sau đó cần quyền mới cùng `Catalog.Update`, so sánh giá trị đang lưu và rowversion trước khi sửa. Nhật ký `ChangeCreditLimit` lưu người thực hiện và hạn mức trước/sau theo định dạng số bất biến; sửa liên hệ với hạn mức không đổi không tạo sự kiện thay đổi hạn mức.
