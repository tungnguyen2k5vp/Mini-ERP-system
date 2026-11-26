# Architecture

MiniErp uses a modular monolith running on ASP.NET Core and a single SQL Server database. One deployment keeps changes that share a business invariant in one transaction. SQL schemas group resources by feature. The React client consumes /api/v1 over its own origin.

- Domain defines business errors and invariant-bearing entities.
- Application exposes use-case contracts, DTOs and policies and depends on Domain.
- Infrastructure implements authentication and persistence, including Identity, EF mapping and migrations.
- API composes the application, translates HTTP requests and errors, issues access tokens and enforces permissions.
- The React client uses TanStack Query for server state and React Hook Form with Zod for form validation.

See [component diagram](../diagrams/COMPONENT_DIAGRAM.md), [database design](../DATABASE_DESIGN.md), [sequence diagrams](../diagrams/SEQUENCE_DIAGRAMS.md) and [decisions](ADR.md).

Orders coordinates StockService and IFinancePosting within one ErpDbContext transaction for receipts, shipments, returns and reversals. A distinct finance transaction locks the affected orders and allocates payments after computing actual collectible or refundable amounts. Reporting uses parameterized SQL over movements, invoices and payments. OrderDemoSeed exercises these same service methods on an isolated development warehouse over six months.
