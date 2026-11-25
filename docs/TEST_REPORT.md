# Test report

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
