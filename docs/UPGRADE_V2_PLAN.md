# MiniErp V2

## Starting point

The baseline is `580a44d` on `feature/erp-operations`. The tree was clean on entry. SQL Server 2022 reports four applied EF migrations through `OrdersAndFinance` on `MiniErp_Test`. READ_COMMITTED_SNAPSHOT is on there, ALLOW_SNAPSHOT_ISOLATION is off. Separate statements under read committed can still see different commits. The existing write paths share an EF transaction for fulfillment, stock, invoice and audit. Finance locks the affected orders before allocating cash.

## Delivery order and acceptance

| Priority and milestone | Depends on | Acceptance |
| --- | --- | --- |
| P0 · 1. Posting and reconciliation | Current schema and source audit | A linked stock document is reversed through its source order. On-hand and reservations reconcile by warehouse/SKU. Invoice sources and allocation discrepancies identify individual records. A clean scope produces zero exceptions. Concurrent postings cannot mix reporting snapshots. Business-day totals include the full Vietnam day. Exercise normal API entry points, rollback-only discrepancy fixtures and restricted roles. |
| P0 · 2. Drafts and approval | Posting invariants | Order and stock draft edits compare rowversions. A transition matrix covers rejection, revision, resubmission, cancellation reasons, actors, expected date and self-approval policy. Old posted documents stay readable. |
| P0 · 3. Exposure and payment corrections | Approval matrix and settlement diagnostics | Lock a customer before approving competing sales. Count net invoiced plus unbilled commitment once. Document zero-limit policy and permissioned overrides. Append linked payment reversals and define dependent refund handling. Test rollback, retries and historical aging. |
| P1 · 4. Quotations and pricing | Orders and monetary policy | One accepted quotation creates one sales order under retry. Persist price list, delivery address, ordered discounts and line-level monetary snapshots. Partial shipments, credits and reversals reconcile to the cent. |
| P1 · 5. Inventory policy and replenishment | Verified reservations and order commitments | Per-warehouse fallback, lead time and targets explain a recommendation. Approved demand is counted once. A planner may create a draft PO. Unit factors are captured on document lines and do not revalue history. |
| P1 · 6–8. BI, UI and performance | Stable source facts, dated contracts and permissions | Reconcile meaningful KPIs and exports to sources. Provide bounded lookups, drill-down, keyboard workflows and permission-aware routes. Review six real viewports and time/report plans on an isolated benchmark database. Record measured capacity. |
| P0/P1 · 9–10. Security and verification | Applied throughout each slice | Check field and endpoint permissions on the server, active-session revocation, workbook size and parser limits. Test transactions, invalid transitions, migration from V1, clean installs, accessibility and Chromium/Firefox/WebKit. |
| P1 · 11. Operations, demo and portfolio | Regression and migration evidence | Verify isolated Compose/CI, restore and deployment rollback procedure. Update bilingual material, diagrams, dated real screenshots and a 10–15 minute demonstrable workflow. |
| P2 · Optional capabilities | P0/P1 evidence and product decision | Escalation workflows, hardware barcodes, attachments and lot/expiry require explicit semantics and tests before changing the schema. |

## First implementation checkpoint

The first delivery is milestone 1. Keep ledger entries, payments and historical invoices immutable. Reconciliation is read-only. Use deterministic comparisons by key so opposite discrepancies cannot cancel each other out. Avoid changing SQL database isolation settings. Keep the existing inclusive timestamp contract in finance and add an explicit business-date boundary for calendar inputs. Record verification and remaining boundaries here and in HANDOVER before advancing the workflow schema.

Milestone 1 implements the linked reversal guard, independent finance and inventory diagnostics, Vietnam calendar filtering, exact revenue attribution and permission-checked financial fields. New sales lines have an immutable two-decimal COGS snapshot. Legacy orders with an unattributed fulfillment keep document-level costing. The migration rehearsal compared SHA-256 content and counts for ten V1 business tables and confirmed all pre-migration line snapshots were null after upgrade.

The next acceptance gate is the milestone 2 draft and approval matrix. Agree the self-approval rule, actor attribution for old rows and handling of partially fulfilled cancellations before introducing new persisted order states. [Verification](TEST_REPORT.md) · [Handover](HANDOVER.md) · [Review findings](CODE_REVIEW_REPORT.md)
