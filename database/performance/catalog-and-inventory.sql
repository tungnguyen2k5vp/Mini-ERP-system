-- Read-only SQL Server query diagnostics on MiniErp.
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
SELECT Id, Sku, Name FROM catalog.Products WHERE Sku = 'VP-01-02';
EXEC reporting.usp_GetInventoryReport @Search = N'VP-01', @Offset = 0, @PageSize = 15;
SET STATISTICS TIME OFF;
SET STATISTICS IO OFF;
GO

SET SHOWPLAN_TEXT ON;
GO
SELECT Id, Sku, Name FROM catalog.Products WHERE Sku = 'VP-01-02';
GO
SET SHOWPLAN_TEXT OFF;
GO
