# Repository references

Review date is 2026-11-25. Architecture and business samples were read as references. MiniErp ships its own domain, API and SQL code. The installed dependency versions are pinned in Directory.Packages.props, frontend/package.json and their lockfiles.

## Architecture, ERP and SQL references

| Source | Revision inspected | License | Application |
| --- | --- | --- | --- |
| [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) | c2781d2 | MIT | Dependency direction and independent Domain and Application projects. |
| [dotnet/eShop](https://github.com/dotnet/eShop) | 3c6e531 | MIT | Catalog and ordering boundaries, validated lifecycle actions and integration-test entry points. |
| [NimblePros/eShopOnWeb](https://github.com/NimblePros/eShopOnWeb) | cc30132 | MIT | One deployment and domain-specific operations with infrastructure outside the domain. |
| [nopCommerce](https://github.com/nopSolutions/nopCommerce) | 0344b61 | nopCommerce Public License | Reference for SKU administration, catalog status, price and access control. |
| [Microsoft SQL Server samples](https://github.com/microsoft/sql-server-samples) | 26dc125 | MIT | WideWorldImporters/AdventureWorks inventory and purchasing/sales data modeling. |
| [SQL Server First Responder Kit](https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit) | 5d3a681 | MIT | Plan, index and logical-read signals. The project uses SQL Server's own read-only tools for its database. |
| [PrismERP](https://github.com/Roger2707/PrismERP) | 116696d | License not verified | Title and README informed comparison of FIFO and concurrency approaches. No code imported. |

The implementation uses a modular monolith, SQL Server and moving-average inventory cost. The models and deployment conventions of the reference projects were evaluated for this scope.

## Installed libraries

| Source | Pinned version | License | Application |
| --- | --- | --- | --- |
| [ASP.NET Core](https://github.com/dotnet/aspnetcore) | 10.0.2 NuGet libraries | MIT | Minimal APIs, Identity, JWT bearer, OpenAPI and WebApplicationFactory. |
| [Entity Framework Core](https://github.com/dotnet/efcore) | 10.0.2 | MIT | Schema migrations, mapped SQL constraints, rowversion and transactional writes. |
| [Dapper](https://github.com/DapperLib/Dapper) | 2.1.66 | Apache-2.0 | Calls the inventory stored procedure and maps result sets. |
| [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient) | 6.1.1 | MIT | SQL Server connections for Dapper and EF Core. |
| [React](https://github.com/facebook/react) | 19.3.1 | MIT | Component and state rendering. |
| [Vite](https://github.com/vitejs/vite) | 7.3.8 | MIT | Development proxy and frontend build. |
| [Tailwind CSS](https://github.com/tailwindlabs/tailwindcss) | 4.3.5 | MIT | CSS build and utility styles. |
| [shadcn/ui](https://github.com/shadcn-ui/ui) | 055aa6b | MIT | Local Button recipe using Slot and class variants. See [notice](SHADCN_LICENSE.txt). |
| [TanStack Query](https://github.com/TanStack/query) | 5.108.4 | MIT | Request lifecycle, cancellation and cache invalidation. |
| [TanStack Table](https://github.com/TanStack/table) | 8.21.3 | MIT | Table rendering, sort and column visibility. |
| [React Router](https://github.com/remix-run/react-router) | 7.18.7 | MIT | Routing and access-aware navigation. |
| [React Hook Form](https://github.com/react-hook-form/react-hook-form) | 7.92.0 | MIT | Forms and stock lines. |
| [React Hook Form resolvers](https://github.com/react-hook-form/resolvers) | 5.2.2 | MIT | Zod form adapter compatible with the pinned ESLint dependency tree. |
| [Zod](https://github.com/colinhacks/zod) | 4.6.7 | MIT | Input schemas and field-level validation. |
| [Recharts](https://github.com/recharts/recharts) | 3.11.1 | MIT | Pinned for the dashboard milestone. |
| [Lucide](https://github.com/lucide-icons/lucide) | 1.80.0 | ISC | UI icons. |
| [Be Vietnam Pro](https://github.com/bettergui/BeVietnamPro) | Fontsource 5.3.0 | OFL-1.1 | Self-hosted Vietnamese UI typography. |
| [xUnit](https://github.com/xunit/xunit) | 2.9.3 | Apache-2.0 | Unit and SQL Server integration checks with VSTest. |
| [Playwright](https://github.com/microsoft/playwright) | 1.65.1 | Apache-2.0 | Real authenticated desktop and mobile flows, screenshots and browser accessibility checks. |
| [Testcontainers for .NET](https://github.com/testcontainers/testcontainers-dotnet) | Repo documentation | MIT | The initial Docker engine was stopped. Tests use isolated MiniErp_Test on the installed SQL Server instance. |

## Agent skills

- The .NET team's repository is the installed source. See [commit 656bed17226a](https://github.com/dotnet/skills/tree/656bed17226aa8ba6d78318fed0bea03f63addd9).
- managedcode/dotnet-skills at 6c69830, Aaronontheweb/dotnet-skills at 02ff3c2, and novotnyllc/dotnet-artisan at 0baeac5 are MIT candidates. The selected official skills covered the current work, so the supplemental catalogs were not installed. [managedcode](https://github.com/managedcode/dotnet-skills) [Aaronontheweb](https://github.com/Aaronontheweb/dotnet-skills) [dotnet-artisan](https://github.com/novotnyllc/dotnet-artisan)
