# Test report

## V2 milestone 2 · 27 November 2026

| Surface | Observed result |
| --- | --- |
| .NET and SQL Server | The build had zero warnings and errors. All 14 unit tests and 59 SQL Server integration tests passed with zero skips. Two Purchasing Staff accounts edited one rowversion through HTTP. Exactly one edit succeeded, the other returned 409, and the persisted header, lines and audit actor matched the winner. A separate writer hit SQL lock timeout 1222 while a caller held an order detail read in a READ COMMITTED transaction, then saved successfully after release. The suite also covered the state matrix, inclusive approval threshold, create/post retries, rollback and scope reuse. |
| Migration with history | Upgraded the five-migration `MiniErp_Test_V2_Browser` to `20261127025043_DocumentWorkflow`. Eleven table projections had identical counts and SHA-256 digests, including rowversions, 312 orders, 397 stock documents, 498 movements, 386 invoices, 208 payments and 2,093 audit events. The separate digest of three existing attributed sales lines also matched. EF reported no pending model changes. |
| Browser | All 38 Playwright cases passed in one local run. Chromium ran 20, Firefox and WebKit ran nine each. Each engine exercised review and resubmission, stale edits in two tabs, recount and cancellation, restricted receipt costs, and saving an open editor after its background GET failed. The last check verified the unsaved note, original version and successful PUT. Axe reported zero violations on the order history and financial reconciliation checks. |
| Responsive UI | The review screen passed page-overflow checks at measured widths of 320, 390, 768, 1024, 1280 and 1440 CSS pixels in all three engines. Editor captures cover 390 and 1280 pixels. The terminal-state screen shows the reason and the action history. [Desktop history](screenshots/v2-workflow-order-workflow-1280.png) · [Mobile history](screenshots/v2-workflow-order-workflow-390.png) · [Order edit](screenshots/v2-workflow-order-editor-1280.png) · [Mobile order edit](screenshots/v2-workflow-order-editor-390.png) · [Stock edit](screenshots/v2-workflow-stock-editor-1280.png) · [Mobile recount](screenshots/v2-workflow-stock-editor-390.png) · [Warehouse receipt edit](screenshots/v2-workflow-warehouse-receipt-editor-390.png) |
| Frontend and configuration | ESLint, TypeScript, three Vitest tests and Vite build passed. Impeccable's changed-file detector returned no findings. The Vite build emitted the two existing upstream Zod annotation notices. Compose configuration resolved both an unset threshold and the supplied test amount. |

The initial test database installation was verified on a separate `MiniErp_Test_M2_Clean_20261127`. The final SQL regression ran there after the complete schema was present. The local browser suite used the upgraded `MiniErp_Test_V2_Browser`. Its background-read test waits for a failed network request before checking the draft and saving it. An earlier synthetic visibility event did not bubble to the query library's window listener. The corrected event passed on all three engines individually and in the full run.

## V2 milestone 1 · 26 November 2026

| Surface | Observed result |
| --- | --- |
| .NET and SQL Server | 12 unit and 47 SQL integration tests passed with zero skips on the migrated test database and on a clean V2 installation. Coverage includes repeat cancellation, linked reversal, exact COGS by SKU, legacy returns, source exceptions, current grants, cent precision, SQL deadlock and HTTP report entry points. |
| Read consistency | The cancellation test blocked a report behind a test transaction, cancelled it, verified `@@TRANCOUNT=0` on the same physical connection and read again. Stock diagnostics catch opposite line-level reservation errors even when aggregate totals match. |
| Local time and money | Posted an order across the last 100 ns before a Vietnam year boundary, at midnight and at the end of the next local day. Verified stock, audit, receipt/issue counts, per-SKU revenue, COGS and finance cutoffs. Split receipts, credits and refunds retained a one-cent receivable/payable balance. |
| Upgrade rehearsal | On `MiniErp_Test_V2_Browser`, compared the same ordered SHA-256 projections of ten business tables before and after migration 5. All matched, including 303 orders, 374 invoices, 202 payments and 474 movements. All 472 prior stock lines kept a null sales-cost snapshot. EF reported no pending model changes. |
| Browser and accessibility | One local run passed all 23 Playwright cases. Chromium ran 15 including the existing commercial flow and Warehouse screen/print price redaction. Firefox and WebKit each ran four P0 tests using real SQL and API responses. The finance reconciliation screen had zero axe violations for the configured WCAG A/AA tags in all three engines. Windows Firefox used software rendering. |
| Responsive UI | Checked stock diagnostics, finance exceptions, aging and dashboard at 320, 390, 768, 1024, 1280 and 1440 measured CSS pixels in each engine. The tests captured 72 views and checked document overflow. These [stock](screenshots/v2-p0-stock-390.png), [finance](screenshots/v2-p0-finance-1280.png) and [dashboard](screenshots/v2-p0-dashboard-1280.png) images come from the passing Chromium run. |
| Frontend | ESLint, TypeScript, 3 Vitest tests and the production Vite build passed. Rollup issued two upstream Zod annotation notices. |

The full V2 roadmap contains later milestones for draft revision, credit limits, payment correction and quotation conversion. The older test counts below refer to V1 checkpoints.

## 26 November 2026 — integrated ERP

| Check | Observed result |
| --- | --- |
| Locked install | Regenerated npm lock metadata in an empty directory after the earlier lock failed online for two missing transitive entries. Final npm ci using Node 24.11.1/npm 11.6.2 installed 325 packages with ESLint 10.15.0. NuGet locked restore succeeded for all six projects. |
| Dependency advisories | npm audit reported zero vulnerabilities. The NuGet vulnerable-package query including transitive packages reported none for all six projects with the online feed. This records feed results on the check date. |
| SQL and model | OrdersAndFinance applied on MiniErp_Test and MiniErp after a copy-only development backup. EF reported no pending model changes. A second history seed reuses stable request IDs and a completion marker. |
| C# and SQL Server | Build passed. 12 xUnit unit tests and 26 SQL Server integration tests passed. Covered competing reservations, over-receipt, replay, stale edits, multi-line rollback, posted returns/reversals, final-cent costing, multi-order allocation, competition for refunds, and customer/supplier resource scoping. |
| Reports and Excel | SQL tests reconcile revenue, profit, historical stock and receivables to source records. XLSX download/import, decimal round-trip, malformed inputs, duplicate SKU, formula rejection and atomic rollback passed. |
| Browser | 10 Playwright tests passed in a combined run with actual Chrome, API and SQL Server. Purchasing tests made two partial receipts, allocated 0.10 + 0.20 and refunded previously paid cash. Sales tests checked reservations and partial shipment. A deliberately lost response produced one order on retry. |
| Frontend | TypeScript, ESLint, 2 Vitest tests and production Vite build passed. Lazy authentication screens reduced the main JS to about 362 kB, 116 kB gzipped. Two vendor annotation notices remain. |
| UI and print | Desktop, 768, 390 and 320 px captures reviewed. Dashboard and mobile login axe checks passed. The PDF generated by Chromium was rendered and inspected as one A4 page. See [UI evidence](UI_BUSINESS_WORKSPACES.md). |
| Development data and recovery | SQL query counted exactly 100 purchase orders, 200 sales orders, 370 invoices/credits and 200 payments in the demo scope from 25 May to 25 November. Restored the pre-upgrade copy to a new MiniErp_Test_Restore database and ran CHECKDB with no errors. Restored copy had 3 migrations, 109 products and 104 movements. |
| SQL scale | On the restored copy, measured an exact SKU lookup, inventory paging and daily movement aggregation with 10,000 added products and 100,000 added ledger rows. Verified original row counts after rollback. [Measurements](SQL_PERFORMANCE.md) |
| Docker and CI | Local Docker Desktop and GitHub Actions both built and ran the Compose stack. Login, stock and dashboard reads and SPA fallback passed. GitHub Actions confirmed 12 unit, 26 SQL integration and 10 Chromium tests. [Linux run](https://github.com/tungnguyen2k5vp/Mini-ERP-system/actions/runs/45761903008) |
| Portfolio SQL | sqlcmd -b executed all 50 read-only exercises against MiniErp_Test successfully. |

Earlier checkpoints below describe their original verification state. Use [deployment](DEPLOYMENT.md) and [SQL performance](SQL_PERFORMANCE.md) for independent container, CI and performance evidence.

| Check | Result |
| --- | --- |
| SQL Server version and database creation permission | Verified via SQLCMD using Windows Authentication, SQL Server 2022 Developer 16.0.1000.6 |
| Milestone 1 EF Core migration | InitialIdentity applied on MiniErp and MiniErp_Test |
| Backend build | dotnet build MiniErp.sln --no-restore, 0 warnings and 0 errors after transitive advisory fixes |
| Backend tests | 1 unit and 3 SQL Server integration tests passed using VSTest and xUnit |
| Frontend | ESLint, TypeScript and Vite build passed. Two Vitest tests passed. |
| Chrome | 3 Playwright tests passed, with real login, bad credentials, cookie refresh, logout, user creation, mobile keyboard focus and viewport overflow checks |
| Accessibility | Axe reported no violations on mobile login for the configured WCAG A/AA tags. Screen reader behavior has no verification result. |
| Docker and GitHub Actions | Pending |

## Milestone 2

- CatalogMasterData applied in both databases. The backend built with zero warnings or errors.
- xUnit passed 1 unit and 5 integration tests. The additional tests verified typed lookup references, unique SKU conflicts, rowversion conflicts, filtering and partner policy checks.
- Vite build, TypeScript, ESLint, 2 Vitest checks and 5 Playwright checks passed. Playwright verified create, deactivate and debounced search against SQL Server, plus mobile navigation to a real customer.
- OpenAPI returned HTTP 200. SQLCMD confirmed 104 seeded products, 8 categories, 3 warehouses, 30 customers and 15 suppliers after rerunning seeding.

## Milestone 3

| Check | Result |
| --- | --- |
| SQL Server | StockLedger applied to MiniErp and MiniErp_Test. The EF model check found no pending changes. Seed repeated successfully. |
| xUnit | 12 passed, 3 unit and 9 SQL Server integration tests. |
| Stock flow | 100 received plus 50 received, then 20 issued and 10 transferred. Main warehouse 120, destination 10, total 130. The test verified average cost 120, repeat posting and linked reversal. |
| Concurrency and rollback | Two concurrent issues of 8 against available 10 produced one success and one conflict. A failing multi-line issue left the original balance, ledger and draft state intact. Stale counts were rejected. |
| Counts and adjustments | Signed adjustments, posting a physical count and cancelling a draft passed. |
| Frontend | ESLint and TypeScript passed, 2 Vitest tests passed and Vite built production assets. The build emits upstream Zod annotation warnings. |
| Browser | 7 Playwright tests passed. The inventory cases verified both transfer ledger rows and reversal, plus mobile low-stock filtering without document overflow. |
| API and SQL reporting | Readiness returned 200 and OpenAPI had 36 paths. SQLCMD executed the schema, constraints and index reports and captured a SKU index seek. |

Run the development processes before scripts/test-all.ps1 -Browser. See [performance evidence](SQL_PERFORMANCE.md) and [screenshots](screenshots/). The reservation, order-to-finance, container, CI and cross-browser checks are still pending.

## Lockfile verification at handover

- NuGet restore in locked mode and the backend build succeeded with zero warnings and errors.
- The full browser and frontend checks passed with the modules already installed. Afterward, a clean setup probe exposed a peer dependency conflict between the optional AJV 8 peer in @hookform/resolvers 5.9.2 and ESLint's AJV 6.
- After pinning @hookform/resolvers to 5.2.2, npm ci --dry-run --offline accepted the new lockfile. A clean reinstall and full frontend rerun using that lockfile remain Not Verified. The combined online setup and restart was denied, and the local npm cache does not have the 5.2.2 tarball. The development preview remains on the prior tested install.
