# API

## V2 milestone 1

| Read | Contract |
| --- | --- |
| `GET /reports/stock/reconciliation` | Current exceptions by warehouse and SKU. Optional `warehouseId`, `productId`, `page`, `pageSize`. Returns ledger quantity, on-hand, recorded reservations, remaining commitment, inconsistent line count and balance presence. An empty page with total zero is a clean scope. |
| `GET /finance/reconciliation/exceptions` | Optional `asOfDate` or `asOf`, `orderId`, `direction`, `page`, `pageSize`. Returns individual source, allocation, refund and authorized cost exceptions, including expected/actual values or references, source IDs and cutoff metadata. |
| `GET /finance/aging` and `/finance/reconciliation` | `asOfDate=YYYY-MM-DD` includes the full Vietnam calendar day. Exact `asOf=...Z` remains inclusive at 100 ns precision. Supplying both returns 400. |
| `GET /reports/dashboard`, `/reports/stock`, `/audit` | Date filters are inclusive local calendar dates, converted to an inclusive UTC start and exclusive UTC end. The dashboard exposes `stockSkuCount` and daily `receiptCount` / `issueCount`; these count original posted receipt/issue documents, including ones subsequently corrected. Transfers, counts, adjustments and reversal documents are excluded from those counts. |

The permissions matrix covers record access, purchase prices, cost, export cells and restricted sorts. [API permissions](API_PERMISSION_MATRIX.md)

Base URL is http://127.0.0.1:5168/api/v1 in local development. OpenAPI is available at [openapi/v1.json](http://127.0.0.1:5168/openapi/v1.json). Use the Vite origin on port 5186 for browser requests. The live and readiness endpoints are /health/live and /health/ready.

| Method and route | Behavior |
| --- | --- |
| POST /auth/login | Validates credentials and creates a session. |
| POST /auth/refresh | Rotates the current refresh token and returns a new JWT. |
| POST /auth/logout | Revokes the current session and clears the cookie. |
| GET /auth/me | Returns the authenticated profile, roles and grants. |
| POST /auth/change-password | Validates the current password and ends existing sessions. |
| GET, POST /users | Searches/paginates users or creates an account. |
| GET, PUT /users/{id} | Reads a user profile or updates role and lock state. |
| GET /roles and /permissions | Returns roles or available permission codes. |
| PUT /roles/{name} | Replaces assignable grants using the role version. |

Cookie-backed POSTs require X-MiniErp-Session equal to 1 and Origin equal to Auth__PublicOrigin. Protected calls carry Authorization: Bearer TOKEN. API errors use Problem Details and X-Correlation-ID. Use the version from the latest response for writes. User paging accepts search, page and pageSize.

See [HTTP requests](../backend/MiniErp.http).

## Commercial documents

| Relative route | Contract and authorization |
| --- | --- |
| GET /orders and /orders/options | `kind=1` for purchase, `kind=2` for sales. Lists accept partner, status, search, sort and pagination. Options belong to the selected module. |
| POST /orders | Create a draft with a UUID requestId, kind, partnerId, warehouseId and lines containing productId, quantity and unitPrice. Requires the corresponding Create grant. |
| GET /orders/{id} | Reads the actual order kind and enforces its View policy. Returns the version, lines and linked fulfillment records. |
| POST /orders/{id}/submit, /approve, /cancel | Body contains the latest version. Approval reserves sales quantities. Cancellation releases the unshipped remainder. |
| POST /orders/{id}/fulfill | Send requestId, isReturn and product/quantity lines. Requires the order module's View and Inventory.Post. Returns linked stock and invoicing results through the detail APIs. |
| POST /orders/{id}/fulfillments/{fulfillmentId}/reverse | Links the inverse operation to the source. A newer position movement can prevent reversal. |
| GET /finance/invoices, /finance/payments, /finance/options | Finance.View. Filter by partner/direction. Use invoiceId to follow an exact invoice. |
| POST /finance/payments | Finance.Post plus Finance.View. Send requestId, partnerId, direction, isRefund, paymentMethodId, amount and an array of invoiceId/amount allocations. |
| GET /finance/aging and /finance/reconciliation | Historical asOf is an ISO timestamp with UTC Z. Receivable direction is 1, payable is 2. |
| GET /reports/dashboard and /reports/stock | Date filters from/to are inclusive UTC calendar dates. Metric visibility follows current permissions. Stock reports reconstruct opening, inflow, outflow and closing and accept warehouseId and filter (all, low, slow). |
| GET /search and /audit | Search only returns accessible records. Audit requires System.Audit and supports date filtering and pagination. |
| GET or POST /catalog/products/workbook | Download an XLSX with Export, or upload multipart field file with Create. Both require Catalog.View. The import creates a whole batch or returns a validation error. |

A successful retry of an unchanged fulfillment or payment returns the existing result. A reused key carrying different input is rejected. Distinct request IDs represent distinct business operations. [Financial settlement](FINANCE.md)

## Catalog

Use GET and POST on /products, /categories, /units, /warehouses, /payment-methods, /customers and /suppliers. GET and PUT on each ID address the detail. Assign active status with PUT and the current version. List filters include search, active, page, pageSize, sort and descending. Products also accept categoryId.

## Inventory

| Method and route | Behavior |
| --- | --- |
| GET /inventory/balances | Paged report with warehouseId, productId, search and lowOnly filters. |
| GET /inventory/history | Paged posted movements with warehouseId or productId filters. |
| GET /inventory/documents | Paged stock documents by kind, status or warehouse. |
| GET /inventory/documents/{id} | Header and line snapshots for a document. |
| POST /inventory/documents | Creates or safely replays a draft by requestId. Kind is 1 receipt, 2 issue, 3 transfer, 4 adjustment or 5 count. |
| POST /inventory/documents/{id}/post | Posts a draft and its effects in a transaction. |
| POST /inventory/documents/{id}/cancel | Cancels a draft. |
| POST /inventory/documents/{id}/reverse | Adds one linked inverse document when every affected position still has the original as its latest movement. |

Inventory view and post actions are enforced by policy. A stock conflict returns HTTP 409 and a machine-readable error code.
