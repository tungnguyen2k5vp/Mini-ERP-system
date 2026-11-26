# Implementation plan

Begin with the Identity session, role policies and a real SQL Server migration. Add catalog and partner records before stock. Connect stock to purchasing (partial receipt) and sales (reservation and shipment) before recording invoices and allocations. Implement reports over the posted records and complete the portfolio material against the resulting source.

Each slice adds the needed domain rules, an EF migration where the model changes, versioned API routes, React screens and tests at the real entry point. See [tasks](TASKS.md) for progress.

## Acceptance gates for the 26 November upgrade

| Milestone | Evidence required |
| --- | --- |
| Baseline | Independently install the npm lockfile, restore locked NuGet dependencies, audit both graphs and pass the existing SQL, frontend and browser checks. |
| Buying and selling | Exercise approval, stale edits, partial fulfillment, return, cancellation and reversal through the HTTP API. Competing orders must preserve available stock. The UI must link each shipment or receipt to its source. |
| Internal finance | Reconcile internal invoices and credits to stock, allocate partial payments across documents, reject duplicate-key mismatches, overpayment and refunds beyond actual cash. Check aging at a cutoff date. |
| Reporting and Excel | Compare reported quantity, revenue, cost, profit and aging with posted records. Exercise role scoping and import all-or-nothing validation. Inspect the main screens at desktop, tablet and phone widths. |
| Operating the system | Rehearse migration and copy-only backup restoration to a new local database. Record real query IO/time at scale, validate Compose, run CI or identify the exact unexecuted stage. |
| Portfolio | Provide repeatable demo, current architecture and API diagrams, source-linked interview exercises, actual screenshots and a handover that distinguishes local evidence from outstanding checks. |
