# Next steps

Begin milestone 4 with the quotation state transitions and the accepted-quotation-to-sales-order transaction. Use the verified M3 baseline and run the existing credit, posting and return regressions after pricing changes. [V2 acceptance criteria](UPGRADE_V2_PLAN.md) · [Evidence](TEST_REPORT.md) · [Permission matrix](API_PERMISSION_MATRIX.md)

M4 should introduce quotations, accepted-quotation retry identity, explicit pricing/discount snapshots and delivery details without changing historical stock or finance. Define monetary behavior and migration acceptance before implementation.

- Use the supplier-to-payment and customer-to-payment walkthroughs in [the demo](DEMO_GUIDE.md). Record follow-up product decisions against observable business rules and the regression suite.
- Inspect the latest [verification](TEST_REPORT.md), [deployment evidence](DEPLOYMENT.md) and [SQL measurements](SQL_PERFORMANCE.md) before repeating environment-dependent checks.
- Order status tracks stock fulfillment. Finance tracks settlement separately. The price snapshot records the agreed unit price, with no separate discount field.
- The portfolio uses internal invoices, order-level credits and keyboard/SKU search. The quotation lifecycle is specified in milestone 4. Hardware barcode management belongs to the inventory roadmap. Statutory accounting and electronic tax invoices remain outside this product.
- For a public deployment, separately authorize the target and verify TLS, environment-specific credentials and recoverability there. The local restore drill and development Compose configuration cover only their named targets.
