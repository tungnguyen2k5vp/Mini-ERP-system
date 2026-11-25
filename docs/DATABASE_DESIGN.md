# Database design

Use a separate SQL Server database for MiniErp development and MiniErp_Test for tests. EF Core migrations under backend/src/MiniErp.Infrastructure/Persistence/Migrations own the schema. See [ERD](diagrams/ERD.md).

The auth schema contains Identity users, roles, memberships, claims, logins, tokens and refresh sessions. Normalized email and refresh token hash are unique. Refresh sessions reference users with restrictive deletion and use rowversion for refresh rotation.

The audit schema contains append-only activity records with UTC datetime2 timestamps, actor ID, correlation ID, action, outcome and optional bounded change details. Passwords and refresh credentials are excluded from snapshots.

A migration may be reapplied safely through the EF migration history mechanism. Development seeding initializes roles and the configured administrator without overwriting the administrator password or edited role grants.

CatalogMasterData introduces typed lookups, products and business partners. A product's composite foreign keys include the expected lookup type. SKU is globally unique and partner code is unique within its kind. Version columns on master data are SQL rowversion. SQL datetime2 values are UTC and HTTP timestamps include the UTC offset.

StockLedger introduces one balance per warehouse and product, document heads and lines, and an immutable movement ledger. Unit cost and quantities use decimal(19,4). Transfers produce two movements with opposite quantities and the same transferred unit cost. A unique document/warehouse/product key prevents repeated movements. Document state and rowversion prevent repeated posting and stale stock writes. The reversal FK links an inverse document to a single original.

Reporting views and procedures are versioned in the same migrations. The inventory view computes Available as OnHand minus Reserved. Low-stock alerts cover active SKUs with a recorded warehouse position. Reservation writes will be connected to sales approval.
