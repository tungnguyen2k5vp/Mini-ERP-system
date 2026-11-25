# MiniErp · checkpoint CV description

## Tiếng Việt

Hệ thống Mini ERP quản lý danh mục và kho nhiều địa điểm cho doanh nghiệp phân phối. Ứng dụng dùng ASP.NET Core, SQL Server và React để ghi sổ nhập, xuất, chuyển, điều chỉnh và kiểm kê kho có truy vết.

## English

Mini ERP for catalog administration and multi-warehouse inventory. It uses an ASP.NET Core modular monolith, SQL Server, and React to track posted receipts, issues, transfers, adjustments, and counts.

## CV bullet points

- Built versioned .NET 10 APIs and a Vietnamese React interface for catalog, partners, role administration and inventory.
- Implemented transactional stock posting, document-linked reversals and moving weighted-average costing with EF Core.
- Enforced SQL Server referential integrity, unique business keys and optimistic concurrency via rowversion.
- Queried a SQL view and parameterized stored procedure using Dapper for inventory balance and low-stock reports.
- Tested authentication, database rules, stock conflicts and browser workflows using xUnit, WebApplicationFactory, SQL Server and Playwright.

See [test results](../TEST_REPORT.md) for verified scenarios and the current checkpoint.
