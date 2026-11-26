-- Run on a disposable copy made by verify-restore.sql; the measured workload rolls back.
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET NOCOUNT ON;
IF LEFT(DB_NAME(), 21) <> 'MiniErp_Test_Restore_' OR LEN(DB_NAME()) <> 53
    THROW 51000, 'Use a disposable restore drill database.', 1;
DECLARE @products bigint = (SELECT COUNT_BIG(*) FROM catalog.Products);
DECLARE @moves bigint = (SELECT COUNT_BIG(*) FROM inventory.Movements);
DECLARE @positions bigint = (SELECT COUNT_BIG(*) FROM inventory.Balances);
DECLARE @warehouse uniqueidentifier = (SELECT TOP (1) Id FROM catalog.Lookups WHERE Kind=3 ORDER BY Code);
DECLARE @category uniqueidentifier = (SELECT TOP (1) Id FROM catalog.Lookups WHERE Kind=1 ORDER BY Code);
DECLARE @unit uniqueidentifier = (SELECT TOP (1) Id FROM catalog.Lookups WHERE Kind=2 ORDER BY Code);
IF @warehouse IS NULL OR @category IS NULL OR @unit IS NULL THROW 51001, 'Seed the source catalog first.', 1;
DECLARE @today date = CONVERT(date,SYSUTCDATETIME());
BEGIN TRY
    BEGIN TRANSACTION;
    CREATE TABLE #Items (Id uniqueidentifier PRIMARY KEY, Sku varchar(32) NOT NULL);
    WITH Digit(n) AS (SELECT n FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) d(n))
    INSERT #Items
        SELECT NEWID(), CONCAT('BENCH-', a.n,b.n,c.n,d.n) FROM Digit a CROSS JOIN Digit b CROSS JOIN Digit c CROSS JOIN Digit d;
    INSERT catalog.Products (Id,Sku,Name,CategoryKind,CategoryId,UnitKind,UnitId,PurchasePrice,SalePrice,MinimumStock,IsActive,CreatedAtUtc,UpdatedAtUtc)
        SELECT Id,Sku,Sku,1,@category,2,@unit,100,125,10,1,@today,@today FROM #Items;
    INSERT inventory.Balances (WarehouseId,WarehouseKind,ProductId,OnHand,Reserved,AverageCost)
        SELECT @warehouse,3,Id,10,0,100 FROM #Items;
    DECLARE @round int=1;
    WHILE @round<=10
    BEGIN
        DECLARE @day datetime2=DATEADD(day,@round-10,CONVERT(datetime2,@today));
        INSERT inventory.Documents (RequestId,Kind,Status,WarehouseKind,WarehouseId,CreatedAtUtc,PostedAtUtc)
            VALUES (NEWID(),1,2,3,@warehouse,@day,@day);
        DECLARE @document int=CONVERT(int,SCOPE_IDENTITY());
        INSERT inventory.DocumentLines (DocumentId,ProductId,Sku,ProductName,Quantity,UnitCost)
            SELECT @document,Id,Sku,Sku,1,100 FROM #Items;
        INSERT inventory.Movements (DocumentId,WarehouseKind,WarehouseId,ProductId,QuantityDelta,UnitCost,PreviousAverageCost,BalanceAfter,PostedAtUtc)
            SELECT @document,3,@warehouse,Id,1,100,IIF(@round=1,0,100),@round,@day FROM #Items;
        SET @round+=1;
    END;
    SELECT COUNT_BIG(*) AS TotalMovements FROM inventory.Movements;
    SET STATISTICS IO ON;
    SET STATISTICS TIME ON;
    PRINT 'Exact business key';
    SELECT Id,Name FROM catalog.Products WHERE Sku='BENCH-5678';
    PRINT 'Inventory procedure, filtered page';
    EXEC reporting.usp_GetInventoryReport @Search=N'BENCH-56',@Offset=0,@PageSize=15;
    PRINT 'Daily movement aggregation, last seven dates';
    SELECT CONVERT(date,PostedAtUtc) AsOfDate, SUM(QuantityDelta) NetMovement
        FROM inventory.Movements WHERE PostedAtUtc >= DATEADD(day,-6,@today)
        GROUP BY CONVERT(date,PostedAtUtc) ORDER BY AsOfDate;
    SET STATISTICS TIME OFF;
    SET STATISTICS IO OFF;
    ROLLBACK;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
IF (SELECT COUNT_BIG(*) FROM catalog.Products)<>@products OR
   (SELECT COUNT_BIG(*) FROM inventory.Balances)<>@positions OR
   (SELECT COUNT_BIG(*) FROM inventory.Movements)<>@moves THROW 51002, 'Rollback verification failed.', 1;
SELECT @products AS OriginalProducts,@positions AS OriginalBalances,@moves AS OriginalMovements;
