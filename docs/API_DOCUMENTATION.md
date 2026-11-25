# API

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
