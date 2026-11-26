# Known issues and verification boundaries

- Browser results are scoped by flow and engine in the [test report](TEST_REPORT.md). Manual screen reader, physical barcode scanner and branded Safari hardware testing are still open. Native date controls follow the browser locale.
- Sales orders with pre-migration fulfillment lines have immutable document-level cost amounts. These orders retain the legacy return-cost policy. Full historical per-SKU COGS attribution is unavailable, and the cost exception check requires complete line snapshots.
- Stock reconciliation compares current state. Historical low-stock status uses the current catalog minimum. Larger-scale BI, credit exposure, draft revision and payment correction have their own acceptance criteria in the [V2 plan](UPGRADE_V2_PLAN.md).
- Vite emits two upstream Rollup annotation warnings in Zod. Both builds and browser verification completed. Native Node 24 and npm 11 were used for clean installation.
- The test runner on Windows needs a SQL-capable Windows identity. The sandbox account cannot authenticate to local SQL Server itself. Integration databases use the MiniErp_Test prefix.
- The historical stock report includes positions with ledger activity, and classifies slow stock by the last movement on or before the cutoff. Current product thresholds are used for historical low-stock classification. The reports do not determine lot-level stock age.
- The workbook importer is create-only. Use the exported header, distinct new SKUs, active lookup codes, General numeric cells and at most 10,000 rows / 2 MB. Importing existing keys returns a row-specific error. Reversals are limited to the latest movement on all affected stock positions.
- Deployment details and the observed Docker/CI state are recorded in [deployment](DEPLOYMENT.md) and the [test report](TEST_REPORT.md).
