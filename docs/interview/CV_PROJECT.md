# MiniErp · checkpoint CV description

## Tiếng Việt

Hệ thống Mini ERP cho doanh nghiệp phân phối. Đơn mua và đơn bán liên kết với tồn kho nhiều địa điểm, hóa đơn nội bộ, công nợ và thanh toán. Có kiểm thử đồng thời và truy vết từ giao dịch đến chứng từ nguồn.

## English

Mini ERP for purchasing, sales, multi-warehouse inventory and operational finance. An ASP.NET Core modular monolith, SQL Server and React track each shipment or receipt through settlement.

## CV bullet points

- Built source-level stock and finance reconciliation in SQL Server, including independent per-line reservation checks, cash-backed refund diagnostics and permissioned cost exceptions.
- Preserved historical ERP records while introducing per-SKU COGS snapshots. Rehearsed the migration on populated V1 data and verified ten business tables by SHA-256.

- Built versioned .NET 10 APIs and a Vietnamese React interface for catalog, partners, role administration and inventory.
- Implemented transactional stock posting, document-linked reversals and moving weighted-average costing with EF Core.
- Enforced SQL Server referential integrity, unique business keys and optimistic concurrency via rowversion.
- Queried a SQL view and parameterized stored procedure using Dapper for inventory balance and low-stock reports.
- Tested authentication, database rules, stock conflicts and browser workflows using xUnit, WebApplicationFactory, SQL Server and Playwright.
- Linked approved orders, partial shipments, stock reservations, returns and internal invoices in a shared SQL transaction. Enforced idempotent posting and tested competing reservations and allocations.
- Built aging, reconciliation and gross-profit reporting from signed documents and recorded inventory costs, including unpaid and cash-backed returns.

See [test results](../TEST_REPORT.md) for verified scenarios and the current checkpoint.
