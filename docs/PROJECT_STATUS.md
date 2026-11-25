# Project status

Updated 2026-11-25. Read [tasks](TASKS.md), [decisions](DECISIONS.md), [next steps](NEXT_STEPS.md) and [test report](TEST_REPORT.md) before continuing.

| Milestone | Status |
| --- | --- |
| 1. Skills, environment, solution, database, authentication | Verified |
| 2. Catalog and partners | Verified for master data |
| 3. Warehouse and inventory | Verified for standalone stock documents |
| 4. Purchasing and receipts | Pending |
| 5. Sales, reservations and shipments | Pending |
| 6. Invoices, payments and returns | Pending |
| 7. Reporting, activity and spreadsheets | Pending |
| 8. Security, performance, Docker and CI | Pending |
| 9. Portfolio, interview and demo | Pending |

SQL Server 2022 Developer is reachable under Windows Authentication outside the Codex sandbox. InitialIdentity was applied to MiniErp and MiniErp_Test. Backend build, xUnit and frontend build passed. Chrome passed desktop and mobile Identity flows and captured real [screenshots](screenshots/). Docker Compose is installed and its engine was stopped at initial inspection.

CatalogMasterData was applied to both databases. API and browser verification covered product creation, typed references, SKU uniqueness, stale edits, partner scoping, access control and deactivation. Spreadsheet workflows and partner transaction history belong to later milestones.

StockLedger was applied to both databases. The stock pipeline, moving average, concurrent issues, rollback and reversal checks passed. Playwright exercised a transfer and its reversal through the running UI and checked mobile stock filtering. Sales-order reservations are part of milestone 5.

The next milestone is purchasing and partial receipt. See [handover](HANDOVER.md), [test results](TEST_REPORT.md) and [next steps](NEXT_STEPS.md).
