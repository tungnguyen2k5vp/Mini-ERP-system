# Database design

## V2 milestone 3

`finance.PaymentReversals` thêm một bản ghi bất biến cho mỗi phiếu bị đảo: Id, PaymentId (FK hạn chế xóa, duy nhất), RequestId (duy nhất), PostedAtUtc, Reason tối đa 300 ký tự và UserId tùy chọn. Phân bổ ngược được suy ra từ phân bổ gốc; không sửa Payments/Allocations và không lưu bản phân bổ âm trùng lặp. Sao kê, tuổi nợ và đối soát áp dụng sự kiện đảo tại thời điểm riêng, giữ nguyên kết quả trước mốc đảo.

Giao dịch tín dụng/thu chi khóa đối tác trước rồi khóa các đơn theo ID tăng dần. Kiểm tra hạn mức dùng công nợ hiện tại và cam kết chưa giao; giá trị 0 là không giới hạn. Thu chi/đảo không được ghi trước sự kiện hóa đơn, thanh toán hoặc đảo liên quan của những đơn bị tác động. Giới hạn thời gian chỉ xét phạm vi nguồn, không áp đặt thứ tự lên các đối tác không liên quan.

Migration `20261126100030_PostedSalesCostSnapshots` adds `inventory.DocumentLines.PostedSalesCost decimal(19,2) NULL` and a nonnegative check. New attributed sales events store a document-line amount in the posting transaction. The column remains null for independent warehouse operations, purchases and unattributed historical orders. Compare V1 and V2 fingerprints with the [read-only rehearsal script](../database/scripts/verify-v2-history.sql).

Use a separate SQL Server database for MiniErp development and MiniErp_Test for tests. EF Core migrations under backend/src/MiniErp.Infrastructure/Persistence/Migrations own the schema. See [ERD](diagrams/ERD.md).

The auth schema contains Identity users, roles, memberships, claims, logins, tokens and refresh sessions. Normalized email and refresh token hash are unique. Refresh sessions reference users with restrictive deletion and use rowversion for refresh rotation.

The audit schema contains append-only activity records with UTC datetime2 timestamps, actor ID, correlation ID, action, outcome and optional bounded change details. Passwords and refresh credentials are excluded from snapshots.

A migration may be reapplied safely through the EF migration history mechanism. Development seeding initializes roles and the configured administrator without overwriting the administrator password or edited role grants.

CatalogMasterData introduces typed lookups, products and business partners. A product's composite foreign keys include the expected lookup type. SKU is globally unique and partner code is unique within its kind. Version columns on master data are SQL rowversion. SQL datetime2 values are UTC and HTTP timestamps include the UTC offset.

StockLedger introduces one balance per warehouse and product, document heads and lines, and an immutable movement ledger. Unit cost and quantities use decimal(19,4). Transfers produce two movements with opposite quantities and the same transferred unit cost. A unique document/warehouse/product key prevents repeated movements. Document state and rowversion prevent repeated posting and stale stock writes. The reversal FK links an inverse document to a single original.

Reporting views and procedures are versioned in the same migrations. The inventory view computes Available as OnHand minus Reserved. Sales approval, cancellation and shipment update the actual reserved balance in the same transaction as order-line reservations.

OrdersAndFinance adds orders, lines and fulfillments in the orders schema and invoices, payments and allocations in the finance schema. Each fulfillment and internal invoice has a unique source stock document. Payment allocations use the payment/invoice pair as key. Two-decimal invoice amounts and four-decimal inventory amounts preserve their distinct business precision. The database uses restrictive historical references. [Financial rules](FINANCE.md)

## V2 milestone 2 document workflow

Orders add nullable `ExpectedDate` (SQL date) and `CreatePayloadHash` (varchar(64)); their state check permits Rejected (7) and ReturnedForRevision (8). Stock documents add nullable `CreatePayloadHash` (varchar(64)) and `IsEdited` (bit, default false). Existing rowversion columns remain the concurrency tokens. Nullable hashes preserve old records; a legacy draft's original normalized create payload is frozen before its first edit. New documents store the hash at creation.

Audit entries add nullable bounded `Reason` (nvarchar(300)) and `InputVersion` (varchar(12)), indexed by Entity/EntityId/Id for document history and exact transition replay. Creator/editor/submitter attribution comes from existing audit user IDs; no maker is backfilled for unknown legacy rows. Names are resolved from current accounts, without changing stored attribution.

Draft edits replace lines inside the document transaction. Posting, reservations, ledger and audit commit together. Document write scopes discard tracked state after completion/rollback; independent detail reads use serializable isolation for coherent header/lines/history. Create-request application locks serialize identical request IDs. Count edits require explicit recount confirmation and capture fresh balance versions. Prior draft line values are not versioned for restoration; hashes support replay identity, not content recovery. Posted ledger, invoices and fulfillment snapshots remain immutable.
