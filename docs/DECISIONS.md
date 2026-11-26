# Decisions

- Use the existing SQL Server 2022 Developer instance with separate MiniErp and MiniErp_Test databases. No runtime installation or service modification is needed for local verification.
- Use one ASP.NET Core process and one SQL Server database. Group by module in code and schema. Keep order documents separate from inventory postings and finance entries so each records the time and quantity of its own event.
- Use EF Core migrations and tracked writes. Use Dapper with SQL views and procedures for reporting reads.
- Use Identity and short-lived JWTs held in memory. Keep the rotating session credential in a browser cookie and retain only its hash in SQL Server.
- Serve browser requests through the Vite development proxy and the production frontend origin. Validate the origin for cookie operations.
- Use an operations-focused Vietnamese UI. Choose a flat, high-contrast utility layout with a deep pine accent and table-first workflows based on the product brief.
- Share a typed lookup table for categories, units, warehouses and payment methods. Composite foreign keys include the lookup kind so SQL Server rejects a unit assigned as a category. Customers and suppliers share a partner table with codes unique per kind.
- Keep the development API executable in .work/api-run so the Windows process does not lock the MSBuild output. Stop only processes that match the MiniErp entry points.
- Use moving average per warehouse and product, rounded to four decimal places. Capture issue cost in the ledger at posting and carry it into the destination for a transfer.
- Reverse only a document that is still the latest movement across every affected position. Later corrections use a new adjustment. A count draft captures the balance versions at entry and requires a recount when stock changes.
- The Inventory.Post grant permits creating, posting, cancelling and reversing warehouse documents. Inventory.View gates the document and balance reads.
- Pin the Hook Form Zod adapter at 5.2.2 and regenerate the npm graph in an empty directory. The verified toolchain uses ESLint 10 and Node 24.
- Purchase and sales share order and fulfillment tables. Kind-scoped policies separate their actions. A credit uses the original order, merchandise quantities and actual posted costing.
- Use a dedicated demo warehouse and a development-only clock to create real historical movements through the same posting, settlement and validation code. Stable request keys and one seed marker protect repeat runs.
- PDF output uses the browser print layout. Product workbooks are ordinary XLSX with bounded ZIP/XML validation. Scanner keyboards can enter the existing SKU search.
