-- reporting.vw_InventorySummary is installed by the StockLedger migration.
SELECT Sku, ProductName, WarehouseName, OnHand, Reserved, Available, AverageCost, IsLowStock
FROM reporting.vw_InventorySummary
WHERE IsLowStock = 1
ORDER BY Sku, WarehouseName;
