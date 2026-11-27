# Tasks

- [x] V2 draft edits, return for revision, resubmission, controlled approval, terminal rejection, cancellation reasons and late-order queues. [Workflow matrix](BUSINESS_WORKFLOWS.md)
- [x] Implement V2 customer exposure, controlled credit-limit edits, immutable payment/refund correction and cutoff statements. [Contracts](FINANCE.md)
- [x] Verify migration from M2 with history, fresh installation, seed replay, SQL concurrency and three browser engines. [M3 evidence](TEST_REPORT.md)
- [ ] M4 quotation lifecycle, accepted quotation conversion, pricing and discount snapshots. [Acceptance criteria](UPGRADE_V2_PLAN.md)

## Earlier V1 delivery evidence (through 27 November 2026)

- [x] Inspect the existing solution, handover and project instructions. Outline acceptance gates in [implementation plan](IMPLEMENTATION_PLAN.md).
- [x] Clean npm and locked NuGet installs. Dependency advisory checks and existing regression suite.
- [x] Purchase and sales orders, approvals, partial receipts/shipments, reservations, cancellation, linked returns and reversals.
- [x] Internal invoices, credit offsets, cash-backed refunds, multi-invoice payments, aging and reconciliation.
- [x] Dashboard, stock and margin reporting, filtered search, audit, notifications, catalog XLSX, A4 output and responsive UI.
- [x] Real six-month demo, diagrams, portfolio guides, interview material and runnable SQL exercises.
- [x] Non-destructive copy-only backup, restore under a new name and database integrity check.
- [x] SQL benchmark with 100,000 added movements in a transaction on a restored copy. [Measurements](SQL_PERFORMANCE.md)
- [x] Compose startup and same-origin login on Windows Docker Desktop and Ubuntu GitHub Actions. [Deployment evidence](DEPLOYMENT.md)

See [test results](TEST_REPORT.md), [SQL evidence](SQL_PERFORMANCE.md) and [remaining scope](NEXT_STEPS.md).
