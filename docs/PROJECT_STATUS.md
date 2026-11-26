# Project status

Updated 2026-11-26. [Test evidence](TEST_REPORT.md) · [Handover](HANDOVER.md) · [Open checks](NEXT_STEPS.md)

| Milestone | Implemented and checked |
| --- | --- |
| 1–3. Foundation, catalog and stock | Existing Identity, catalog, partner, inventory and reversal regression suites pass against SQL Server and Chrome. |
| 4. Purchasing | Approval, partial receipt, over-receipt rejection, return, reversal, supplier history and source-linked payables. |
| 5. Sales | Atomic reservations, competing approvals, partial shipment, release on cancellation, returns and recorded shipment cost. |
| 6. Internal finance | Invoice-per-movement, credit offsets, cash-backed refunds, multi-invoice allocation, concurrency, aging and reconciliation. |
| 7. Reports and UI | Role-specific dashboard, product ranking and gross margin, historical stock with low/slow filters, audit, search, workbook validation and printable documents. |
| 8. Operations | Locked installs, advisory checks, structured logs, a copy-only restore rehearsal, a 100,000-movement SQL benchmark and verified Compose startup on Windows and Ubuntu CI. |
| 9. Portfolio | English/Vietnamese README, workflow and ERD diagrams, screenshots, real PDF, CV notes, 100 backend interview questions, 25 React prompts and 50 SQL exercises. |

The 6-month commercial demo contains 100 purchase orders, 200 sales orders, 370 internal invoices/credits and 200 partial payments. Each is generated through the normal application services. An isolated warehouse and deterministic command IDs keep it separate from existing stock.

[Local and container deployment](DEPLOYMENT.md) · [SQL performance](SQL_PERFORMANCE.md) · [Known issues](KNOWN_ISSUES.md)
