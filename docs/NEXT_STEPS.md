# Next steps

Continue with P0 milestone 3 in the [V2 plan](UPGRADE_V2_PLAN.md). Lock the customer before approval, calculate outstanding receivables plus approved unbilled commitments, and test concurrent competing approvals. Define the zero-limit policy and the treatment of later credit/refund dependencies before adding immutable payment reversals. The milestone 2 workflow uses six migrations and the [document transition matrix](BUSINESS_WORKFLOWS.md).

- Use the supplier-to-payment and customer-to-payment walkthroughs in [the demo](DEMO_GUIDE.md). Record follow-up product decisions against observable business rules and the regression suite.
- Inspect the latest [verification](TEST_REPORT.md), [deployment evidence](DEPLOYMENT.md) and [SQL measurements](SQL_PERFORMANCE.md) before repeating environment-dependent checks.
- Order status tracks stock fulfillment. Finance tracks settlement separately. The price snapshot records the agreed unit price, with no separate discount field.
- The portfolio scope uses internal invoices, order-level credits and a keyboard/SKU search. A dedicated quotation lifecycle, hardware barcode management, statutory accounting and electronic tax invoices would each require additional product requirements.
- For a public deployment, separately authorize the target and verify TLS, environment-specific credentials and recoverability there. The local restore drill and development Compose configuration cover only their named targets.
