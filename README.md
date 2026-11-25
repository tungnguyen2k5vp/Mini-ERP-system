# MiniErp

A Vietnamese operations application for small distributors. It uses a .NET 10 modular monolith, SQL Server 2022 and a React/TypeScript client. The goal is to trace operational documents, inventory and internal finance from the same underlying records.

The current checkpoint covers authentication, master data and multi-warehouse stock operations. The UI calls the running API, which records changes in SQL Server. See the [handover](docs/HANDOVER.md) for verification results and remaining milestones.

## Available workflows

- Sign in, refresh a session, change a password and administer accounts, roles and permission grants.
- Search, filter, create, edit and deactivate catalog and partner records.
- Draft, post and reverse receipts, issues, transfers, adjustments and counts. View stock balances, low-stock positions and movements with their recorded costs.

| Desktop inventory | Mobile inventory |
| --- | --- |
| ![Stock balances on desktop](docs/screenshots/inventory-desktop.png) | ![Stock balances on mobile](docs/screenshots/inventory-mobile.png) |

## Local setup

From D:/ReactJS/MiniErp in PowerShell:

~~~powershell
.\scripts\check-environment.ps1
.\scripts\setup.ps1
.\scripts\migrate-db.ps1
.\scripts\seed-demo.ps1
.\scripts\start-dev.ps1
~~~

Open the [application](http://localhost:5186). The development seed creates admin@minierp.example using the password in the local, ignored .work/dev.env.json. OpenAPI is available at [openapi/v1.json](http://127.0.0.1:5168/openapi/v1.json). Use scripts/stop-dev.ps1 to stop this project's two development processes. The database is retained.

## Architecture and tests

- [Architecture](docs/architecture/ARCHITECTURE.md) and [database ERD](docs/diagrams/ERD.md)
- [Deployment and configuration](docs/DEPLOYMENT.md), [API](docs/API_DOCUMENTATION.md) and [security](docs/SECURITY.md)
- [Actual build and test results](docs/TEST_REPORT.md) and [known issues](docs/KNOWN_ISSUES.md)
- [Repository references](docs/REPOSITORY_REFERENCES.md) and [.NET skill inventory](docs/SKILLS_INVENTORY.md)
- [Demo](docs/DEMO_GUIDE.md), [SQL performance](docs/SQL_PERFORMANCE.md) and [CV notes](docs/interview/CV_PROJECT.md)

Run scripts/test-all.ps1 to use the isolated MiniErp_Test database and check the frontend. Add -Browser after setting up the running development services to exercise the real UI with Playwright.

## License

MiniErp is provided under the [MIT license](LICENSE). See the [repository references](docs/REPOSITORY_REFERENCES.md) for third-party attribution.
