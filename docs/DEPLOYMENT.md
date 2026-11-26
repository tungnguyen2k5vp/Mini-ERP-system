# Local deployment and CI

The PowerShell development workflow uses the existing Windows SQL Server instance. Docker Compose is a separate **local development** option with its own SQL data volume. These recipes do not establish production readiness.

## Existing Windows development workflow

Install the .NET SDK selected by `global.json`, Node.js 24, and a dedicated SQL Server development instance. From the repository root:

```powershell
.\scripts\setup.ps1
.\scripts\migrate-db.ps1
.\scripts\seed-demo.ps1
.\scripts\start-dev.ps1
```

The frontend is http://localhost:5186; the API listens on http://127.0.0.1:5168. Credentials remain in the ignored `.work/dev.env.json`. Logs are `.work/api.log` and `.work/frontend.log`. Use `scripts/stop-dev.ps1` before restarting or using the same frontend port for Docker. Startup preserves the database.

`scripts/test-all.ps1` uses only `MiniErp_Test` by default. `MINIERP_TEST_CONNECTION` may select another isolated `MiniErp_Test_*` database; the script rejects other database names. The Windows scripts intentionally configure local Windows Authentication. For other hosts, pass configuration directly to the executable rather than sourcing `local-env.ps1`.

## Docker development

Requirements: Docker Engine with Linux containers, Compose supporting `service_completed_successfully`, at least 2 GB available for SQL Server (4 GB+ overall recommended), and an x86-64 host supported by the SQL Server Linux image. The SQL Server Developer image is for development/test use.

Copy `.env.example` to the ignored `.env`, then fill every blank value with independently generated credentials. Do not commit this file. Required configuration:

| Variable | Requirement |
| --- | --- |
| `MSSQL_SA_PASSWORD` | Strong SQL administrator password, 16+ characters; avoid semicolon, quote and dollar sign because it is used in a connection string. |
| `ERP_DB_PASSWORD` | Separate runtime SQL password: 16-128 letters/digits or `@%_+=:,.-`, including uppercase, lowercase, digit and symbol. The provisioner validates the alphabet before using a SQL literal. |
| `ERP_SIGNING_KEY` | Base64 of at least 32 cryptographically random bytes. |
| `ERP_ADMIN_EMAIL` | Development administrator email. |
| `ERP_ADMIN_PASSWORD` | Strong unique development administrator password meeting Identity rules. |
| `ERP_PUBLIC_ORIGIN` | Exact browser origin without trailing slash; default `http://localhost:5186`. |

Quote values in `.env` with single quotes when they contain characters with Compose interpolation meaning. Compose fails before starting when required values are missing. The API additionally validates signing-key length, origin and administrator credentials. Do not print `docker compose config` without `--quiet`, because expanded configuration contains secrets.

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose ps --all
Invoke-WebRequest http://localhost:5186/health/live
Invoke-WebRequest http://localhost:5186/health/ready
```

Open http://localhost:5186 and sign in using `ERP_ADMIN_EMAIL` / `ERP_ADMIN_PASSWORD`. Check the dashboard, a purchase receipt, sales shipment and finance allocation through the ordinary UI. Refresh a nested route such as `/bao-cao` to verify the SPA fallback.

The dependency sequence is explicit:

1. `sql` starts with a persistent `sql-data` volume and a real `SELECT 1` healthcheck.
2. One-shot `migrate` applies existing EF migrations and idempotently seeds the development catalog/stock using the SQL administrator credential. It exits instead of serving requests.
3. One-shot `provision` creates/updates the dedicated `minierp_app` login and grants only the schema operations used by the application.
4. `api` starts with that runtime login, without administrator credentials or migration flags. Its readiness check requires SQL connectivity and no pending migrations.
5. Unprivileged `web` serves the built SPA and proxies `/api/` and `/health/` to the private API. Its healthcheck requests both the SPA and database readiness endpoint.

Only `127.0.0.1:5186` is published. SQL and API have no host port. API and web run without root, drop Linux capabilities and disable privilege escalation. Runtime SQL has no DDL grant, no finance/audit update or delete grant, and only the auth module receives delete permission for role membership/claim changes. Migration access to `sa` is isolated to the one-shot service; SQL itself and the provisioner necessarily also receive the administrator credential.

This recipe deliberately sets `Development` to support loopback HTTP and development seed data. It is unsuitable for public hosting unchanged. SQL transport uses a trusted development certificate. A real deployment needs reviewed TLS termination, forwarded-header trust, certificate validation, managed secrets, image digests/update policy, backups, restricted network exposure and separately approved migration execution.

To inspect startup failures, use `docker compose logs migrate provision api web`. Do not share logs or environment inspection output without checking for secrets. `docker compose down` stops services and preserves the named SQL volume. Do not add `--volumes` unless intentionally destroying that disposable development database. Changing the SA password in `.env` does not rotate the password stored in an existing SQL volume; rotate it explicitly using SQL Server administration. The provisioner can rotate the restricted runtime login password during the next startup.

## GitHub Actions

`.github/workflows/ci.yml` runs on pushes, pull requests and manual dispatch. It requests only `contents: read`, cancels superseded branch runs and publishes no images or deployments. It generates fresh masked SQL, application and signing credentials inside each disposable job; no repository credential is required.

The job starts SQL Server on runner loopback, waits for an actual query, performs locked NuGet restore and `npm ci --ignore-scripts`, builds/tests the backend against `MiniErp_Test_CI`, and runs frontend lint/type checks/unit tests/build. Browser tests use a separately migrated and seeded `MiniErp_Test_Browser` database and the normal API/Vite entry points. The entire Playwright suite runs, including existing identity/catalog/inventory and purchasing/sales/finance/report flows and mobile accessibility checks.

The workflow builds the images and starts the full Compose stack on runner loopback. It verifies SQL readiness through nginx, signs in, reads stock and dashboard data through the restricted runtime SQL login and opens a nested SPA URL. Artifacts retain backend TRX results, API/web/SQL logs, Playwright reports and screenshots for seven days. Hosted runners discard their temporary container storage after each job.

## Verification status

- On 26 November 2026 the isolated `minierp-verify` project built both images and ran on Windows Docker Desktop. The migration and runtime provisioning services exited successfully. API and web readiness passed. Requests through port 5198 returned HTTP 200 for sign-in, eight dashboard metrics, 120 warehouse positions and the `/mua-hang` SPA route. The services were stopped and `minierp-verify_sql-data` was retained.
- The Ubuntu runner passed all stages, including image builds, migrations, browser tests and the Compose runtime checks. [GitHub Actions run 45761903008](https://github.com/tungnguyen2k5vp/Mini-ERP-system/actions/runs/45761903008)
- See the host checks, operating limits and backup/restore rehearsal in the [test report](TEST_REPORT.md).
