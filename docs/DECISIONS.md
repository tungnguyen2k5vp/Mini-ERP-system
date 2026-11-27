# Decisions

## V2 reporting and costing

- Vietnam operational periods use UTC+07:00. Keep UTC event timestamps and range predicates on the original timestamp columns. Perform calendar-day grouping only after filtering.
- On new sales, persist each stock line’s two-decimal posted COGS under the order lock in the posting transaction. A partial return receives its proportional share of the remaining recorded SKU cost. Returning the last unit of that SKU releases its exact residual. A reversal copies the original snapshots. Warehouse moving average continues to use four decimal places.
- Retain document-level costing for any order with an unattributed historical sales fulfillment. The migration adds a nullable column with a nonnegative check. Source-cost diagnostics skip incomplete line attribution. No historical allocation is inferred.
- For V1 commercial revenue, reconstruct the original line amount as the difference between two cumulative rounded amounts for the same order, SKU and shipment/return stream. Reverse the source amount verbatim. This keeps a rounding cent with the actual product on a later multi-line shipment.
- Use serializable reads where a report compares multiple sources or result sets. Materialize expensive reusable groups for reconciliation. Binder parameters for exact finance cutoffs use `datetime2(7)`. SQL money aggregates are narrowed to `decimal(28,2)` before subtraction so SQL Server retains cents.
- Cost visibility uses existing grants. Procurement and catalog price entry, historical stock valuation and internal financial balances serve different business tasks. [Permission matrix](API_PERMISSION_MATRIX.md)

- Use the existing SQL Server 2022 Developer instance with separate MiniErp and MiniErp_Test databases. No runtime installation or service modification is needed for local verification.
- Use one ASP.NET Core process and one SQL Server database. Group by module in code and schema. Keep order documents separate from inventory postings and finance entries so each records the time and quantity of its own event.
- Use EF Core migrations and tracked writes. Use Dapper with SQL views and procedures for reporting reads.
- Use Identity and short-lived JWTs held in memory. Keep the rotating session credential in a browser cookie and retain only its hash in SQL Server.
- Serve browser requests through the Vite development proxy and the production frontend origin. Validate the origin for cookie operations.
- Use an operations-focused Vietnamese UI. Choose a flat, high-contrast utility layout with a deep pine accent and table-first workflows based on the product brief.
- Share a typed lookup table for categories, units, warehouses and payment methods. Composite foreign keys include the lookup kind so SQL Server rejects a unit assigned as a category. Customers and suppliers share a partner table with codes unique per kind.
- Keep the development API executable in .work/api-run so the Windows process does not lock the MSBuild output. Stop only processes that match the MiniErp entry points.
- Use moving average per warehouse and product, rounded to four decimal places. Capture issue cost in the ledger at posting and carry it into the destination for a transfer.
- Reverse only a document that is still the latest movement across every affected position. Later corrections use a new adjustment. A count draft captures the balance versions at entry and requires a recount when stock changes.
- The Inventory.Post grant permits creating, editing drafts, posting, cancelling and reversing warehouse documents. Inventory.View gates the document and balance reads.
- Pin the Hook Form Zod adapter at 5.2.2 and regenerate the npm graph in an empty directory. The verified toolchain uses ESLint 10 and Node 24.
- Purchase and sales share order and fulfillment tables. Kind-scoped policies separate their actions. A credit uses the original order, merchandise quantities and actual posted costing.
- Use a dedicated demo warehouse and a development-only clock to create real historical movements through the same posting, settlement and validation code. Stable request keys and one seed marker protect repeat runs.
- PDF output uses the browser print layout. Product workbooks are ordinary XLSX with bounded ZIP/XML validation. Scanner keyboards can enter the existing SKU search.

## V2 draft and approval policy

- Separate maker and approver using audit actor IDs: creator, latest editor and latest submitter cannot approve without `Orders.SelfApprove`. Unknown legacy creators conservatively require that override. Do not fabricate maker IDs during migration.
- Configure an optional positive `Orders__ApprovalThresholdVnd`; rounded order totals at or above it require `Orders.ApproveAboveLimit` in addition to module approval. Leaving it unset disables the amount gate. Neither override is a default Manager grant.
- Draft/returned orders can be edited, returned orders must change before resubmission, and rejected orders are terminal. Require reasons for rejection, revision and cancellation. Cancelling a partial order releases only remaining reservations and preserves shipped quantities and invoices.
- Compare rowversions on draft writes. Preserve normalized original create fingerprints across edits, including legacy drafts. Exact transition retries compare the submitted version, actor and reason; avoid repeated audit or ledger effects. A draft PUT with an uncertain result requires reload rather than automatic content restoration.
- Keep hidden stock costs server-owned during edit: retained products preserve omitted/null values; new restricted receipt products use their own current purchase-price snapshot. Explicit edit costs require valuation visibility. Receipt creation retains its existing explicit cost rule.
- Require explicit fresh-count confirmation for every count edit. Read document headers, lines and history coherently, and isolate tracked write state after rollback. History exposes action/UTC actor/reason metadata; it does not retain old draft contents for restoration.
