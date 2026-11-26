# Known issues and verification boundaries

- Chromium/Chrome, keyboard flows and automated axe checks were exercised. Manual screen reader, Firefox, Safari and physical barcode scanners have no recorded verification. Native date fields follow the browser locale.
- Vite emits two upstream Rollup annotation warnings in Zod. Both builds and browser verification completed. Native Node 24 and npm 11 were used for clean installation.
- The test runner on Windows needs a SQL-capable Windows identity. The sandbox account cannot authenticate to local SQL Server itself. Integration databases use the MiniErp_Test prefix.
- The historical stock report includes positions with ledger activity, and classifies slow stock by the last movement on or before the cutoff. Current product thresholds are used for historical low-stock classification. The reports do not determine lot-level stock age.
- The workbook importer is create-only. Use the exported header, distinct new SKUs, active lookup codes, General numeric cells and at most 10,000 rows / 2 MB. Importing existing keys returns a row-specific error. Reversals are limited to the latest movement on all affected stock positions.
- Deployment details and the observed Docker/CI state are recorded in [deployment](DEPLOYMENT.md) and the [test report](TEST_REPORT.md).