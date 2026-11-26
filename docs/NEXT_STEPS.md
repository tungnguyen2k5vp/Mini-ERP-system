# Next steps

Continue with P0 milestone 2 in the [V2 plan](UPGRADE_V2_PLAN.md). First map the order and stock transitions, draft rowversions, ownership, rejection reasons and self-approval policy. Then implement the customer credit lock and immutable payment corrections in milestone 3. Verify migration from the five-migration checkpoint before advancing to quotations or UOM.

- Use the supplier-to-payment and customer-to-payment walkthroughs in [the demo](DEMO_GUIDE.md). Record follow-up product decisions against observable business rules and the regression suite.
- Inspect the latest [verification](TEST_REPORT.md), [deployment evidence](DEPLOYMENT.md) and [SQL measurements](SQL_PERFORMANCE.md) before repeating environment-dependent checks.
- Order status tracks stock fulfillment. Finance tracks settlement separately. The price snapshot records the agreed unit price, with no separate discount field.
- The portfolio scope uses internal invoices, order-level credits and a keyboard/SKU search. A dedicated quotation lifecycle, hardware barcode management, statutory accounting and electronic tax invoices would each require additional product requirements.
- For a public deployment, separately authorize the target and verify TLS, environment-specific credentials and recoverability there. The local restore drill and development Compose configuration cover only their named targets.
