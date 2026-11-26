-- MiniErp: 50 read-only SQL Server exercises and reference solutions.
-- Select an authorized MiniErp development/test database in your SQL client.
-- Requires OrdersAndFinance plus earlier EF migrations. No persistent writes.
-- Result-set order follows the numbered exercises. All timestamps are UTC.
DECLARE @AsOf datetime2 = SYSUTCDATETIME();
DECLARE @From datetime2 = DATEADD(day,-30,@AsOf);
DECLARE @To datetime2 = @AsOf;

-- Exercise 01: Count products by active state.
SELECT IsActive,COUNT_BIG(*) ProductCount FROM catalog.Products GROUP BY IsActive;

-- Exercise 02: Inspect current product prices and reference codes.
SELECT TOP (200) p.Sku,p.Name,c.Code CategoryCode,u.Code UnitCode,p.PurchasePrice,p.SalePrice
FROM catalog.Products p JOIN catalog.Lookups c ON c.Id=p.CategoryId AND c.Kind=p.CategoryKind
JOIN catalog.Lookups u ON u.Id=p.UnitId AND u.Kind=p.UnitKind ORDER BY p.Sku;

-- Exercise 03: Find duplicate SKUs; success means no rows.
SELECT Sku,COUNT_BIG(*) Copies FROM catalog.Products GROUP BY Sku HAVING COUNT_BIG(*)>1;

-- Exercise 04: Count reference records by kind and state (1 category,2 unit,3 warehouse,4 payment method).
SELECT Kind,IsActive,COUNT_BIG(*) Records FROM catalog.Lookups GROUP BY Kind,IsActive ORDER BY Kind,IsActive;

-- Exercise 05: Find active products whose category or unit is inactive.
SELECT TOP (200) p.Sku,c.Code CategoryCode,c.IsActive CategoryActive,u.Code UnitCode,u.IsActive UnitActive
FROM catalog.Products p JOIN catalog.Lookups c ON c.Id=p.CategoryId JOIN catalog.Lookups u ON u.Id=p.UnitId
WHERE p.IsActive=1 AND (c.IsActive=0 OR u.IsActive=0) ORDER BY p.Sku;

-- Exercise 06: Count customers and suppliers by active state (1 customer,2 supplier).
SELECT Kind,IsActive,COUNT_BIG(*) PartnerCount FROM catalog.BusinessPartners GROUP BY Kind,IsActive;

-- Exercise 07: Inspect payment terms without exposing contact data.
SELECT TOP (200) Code,Name,Kind,PaymentTermsDays,CreditLimit FROM catalog.BusinessPartners ORDER BY Kind,Code;

-- Exercise 08: Display stock availability separately from physical stock.
SELECT TOP (200) w.Code Warehouse,p.Sku,b.OnHand,b.Reserved,b.OnHand-b.Reserved Available,b.AverageCost
FROM inventory.Balances b JOIN catalog.Lookups w ON w.Id=b.WarehouseId JOIN catalog.Products p ON p.Id=b.ProductId
ORDER BY w.Code,p.Sku;

-- Exercise 09: List low stock positions using available quantity.
SELECT TOP (200) w.Code Warehouse,p.Sku,b.OnHand-b.Reserved Available,p.MinimumStock
FROM inventory.Balances b JOIN catalog.Products p ON p.Id=b.ProductId JOIN catalog.Lookups w ON w.Id=b.WarehouseId
WHERE b.OnHand-b.Reserved<=p.MinimumStock AND p.IsActive=1 ORDER BY Available,p.Sku;

-- Exercise 10: Calculate current stock value per warehouse.
SELECT w.Code Warehouse,SUM(b.OnHand*b.AverageCost) StockValue
FROM inventory.Balances b JOIN catalog.Lookups w ON w.Id=b.WarehouseId GROUP BY w.Code ORDER BY w.Code;

-- Exercise 11: Find impossible balances; success means no rows.
SELECT WarehouseId,ProductId,OnHand,Reserved,AverageCost FROM inventory.Balances
WHERE OnHand<Reserved OR Reserved<0 OR AverageCost<0;

-- Exercise 12: Count stock documents by kind and status.
SELECT Kind,Status,COUNT_BIG(*) Documents FROM inventory.Documents GROUP BY Kind,Status ORDER BY Kind,Status;

-- Exercise 13: Review recent source document movements in a half-open UTC interval.
SELECT TOP (200) Id,DocumentId,WarehouseId,ProductId,QuantityDelta,UnitCost,BalanceAfter,PostedAtUtc
FROM inventory.Movements WHERE PostedAtUtc>=@From AND PostedAtUtc<@To ORDER BY PostedAtUtc DESC,Id DESC;

-- Exercise 14: Reconcile OnHand to all movement deltas; success means no rows.
WITH Ledger AS (SELECT WarehouseId,ProductId,SUM(QuantityDelta) Quantity FROM inventory.Movements GROUP BY WarehouseId,ProductId)
SELECT COALESCE(b.WarehouseId,l.WarehouseId) WarehouseId,COALESCE(b.ProductId,l.ProductId) ProductId,b.OnHand,l.Quantity
FROM inventory.Balances b FULL JOIN Ledger l ON l.WarehouseId=b.WarehouseId AND l.ProductId=b.ProductId
WHERE COALESCE(b.OnHand,0)<>COALESCE(l.Quantity,0);

-- Exercise 15: Verify every recorded BalanceAfter against the running ledger; success means no rows.
WITH Running AS (SELECT Id,WarehouseId,ProductId,BalanceAfter,
SUM(QuantityDelta) OVER(PARTITION BY WarehouseId,ProductId ORDER BY Id ROWS UNBOUNDED PRECEDING) Expected
FROM inventory.Movements)
SELECT TOP (200) * FROM Running WHERE BalanceAfter<>Expected ORDER BY Id;

-- Exercise 16: Get the latest movement for each stock position.
WITH Ranked AS (SELECT Id,WarehouseId,ProductId,QuantityDelta,BalanceAfter,PostedAtUtc,
ROW_NUMBER() OVER(PARTITION BY WarehouseId,ProductId ORDER BY Id DESC) rn FROM inventory.Movements)
SELECT TOP (200) Id,WarehouseId,ProductId,QuantityDelta,BalanceAfter,PostedAtUtc FROM Ranked WHERE rn=1 ORDER BY Id DESC;

-- Exercise 17: Calculate receipts/issues during the interval, including reversal effects as signed deltas.
SELECT WarehouseId,ProductId,SUM(CASE WHEN QuantityDelta>0 THEN QuantityDelta ELSE 0 END) InQuantity,
SUM(CASE WHEN QuantityDelta<0 THEN -QuantityDelta ELSE 0 END) OutQuantity
FROM inventory.Movements WHERE PostedAtUtc>=@From AND PostedAtUtc<@To GROUP BY WarehouseId,ProductId;

-- Exercise 18: Reconstruct opening, movement and closing quantity for each position.
SELECT WarehouseId,ProductId,SUM(CASE WHEN PostedAtUtc<@From THEN QuantityDelta ELSE 0 END) Opening,
SUM(CASE WHEN PostedAtUtc>=@From AND PostedAtUtc<@To THEN QuantityDelta ELSE 0 END) Change,
SUM(CASE WHEN PostedAtUtc<@To THEN QuantityDelta ELSE 0 END) Closing
FROM inventory.Movements WHERE PostedAtUtc<@To GROUP BY WarehouseId,ProductId;

-- Exercise 19: List original/reversal stock document links.
SELECT TOP (200) original.Id OriginalId,original.Kind,original.Status,reversal.Id ReversalId,reversal.PostedAtUtc
FROM inventory.Documents original JOIN inventory.Documents reversal ON reversal.ReversesId=original.Id ORDER BY reversal.Id DESC;

-- Exercise 20: Verify original and reversal quantities net to zero by position; success means no rows.
SELECT o.Id OriginalId,m.WarehouseId,m.ProductId,SUM(m.QuantityDelta) NetDelta
FROM inventory.Documents o JOIN inventory.Documents r ON r.ReversesId=o.Id
JOIN inventory.Movements m ON m.DocumentId=o.Id OR m.DocumentId=r.Id
GROUP BY o.Id,m.WarehouseId,m.ProductId HAVING SUM(m.QuantityDelta)<>0;

-- Exercise 21: Find positive stock positions with no movement in the last 90 days.
SELECT TOP (200) b.WarehouseId,b.ProductId,b.OnHand,MAX(m.PostedAtUtc) LastMovementAtUtc
FROM inventory.Balances b LEFT JOIN inventory.Movements m ON m.WarehouseId=b.WarehouseId AND m.ProductId=b.ProductId
WHERE b.OnHand>0 GROUP BY b.WarehouseId,b.ProductId,b.OnHand
HAVING MAX(m.PostedAtUtc)<DATEADD(day,-90,@AsOf) OR MAX(m.PostedAtUtc) IS NULL;

-- Exercise 22: Count purchase/sales orders by workflow state (kind1 PO,kind2 SO; pending state2).
SELECT Kind,Status,COUNT_BIG(*) Orders FROM orders.Orders GROUP BY Kind,Status ORDER BY Kind,Status;

-- Exercise 23: Display canonical order numbers and rounded commercial totals.
SELECT TOP (200) o.Id,CONCAT(CASE WHEN o.Kind=1 THEN 'PO-' ELSE 'SO-' END,
CASE WHEN o.Id<1000000 THEN RIGHT(CONCAT('000000',o.Id),6) ELSE CONVERT(varchar(12),o.Id) END) Number,
p.Name Partner,o.Status,SUM(ROUND(l.Quantity*l.UnitPrice,2)) OrderedTotal
FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id JOIN catalog.BusinessPartners p ON p.Id=o.PartnerId
GROUP BY o.Id,o.Kind,p.Name,o.Status ORDER BY o.Id DESC;

-- Exercise 24: List approved/partial order quantities still outstanding.
SELECT TOP (200) o.Id,o.Kind,l.Sku,l.Quantity,l.FulfilledQuantity,l.Quantity-l.FulfilledQuantity Remaining
FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id
WHERE o.Status IN (3,4) AND l.Quantity>l.FulfilledQuantity ORDER BY o.Id,l.Sku;

-- Exercise 25: Reconcile order reservations to stock reservations; success means no rows.
WITH R AS (SELECT o.WarehouseId,l.ProductId,SUM(l.ReservedQuantity) Reserved
FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id WHERE o.Kind=2 GROUP BY o.WarehouseId,l.ProductId)
SELECT COALESCE(b.WarehouseId,r.WarehouseId) WarehouseId,COALESCE(b.ProductId,r.ProductId) ProductId,
COALESCE(b.Reserved,0) BalanceReserved,COALESCE(r.Reserved,0) OrderReserved
FROM inventory.Balances b FULL JOIN R r ON r.WarehouseId=b.WarehouseId AND r.ProductId=b.ProductId
WHERE COALESCE(b.Reserved,0)<>COALESCE(r.Reserved,0);

-- Exercise 26: Find invalid order-line quantities; success means no rows.
SELECT OrderId,ProductId,Quantity,FulfilledQuantity,ReturnedQuantity,ReservedQuantity FROM orders.OrderLines
WHERE Quantity<=0 OR FulfilledQuantity<0 OR FulfilledQuantity>Quantity OR ReturnedQuantity<0
OR ReturnedQuantity>FulfilledQuantity OR ReservedQuantity<0 OR ReservedQuantity>Quantity-FulfilledQuantity;

-- Exercise 27: Find cancelled orders retaining reservations; success means no rows.
SELECT o.Id,l.ProductId,l.ReservedQuantity FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id
WHERE o.Status=6 AND l.ReservedQuantity<>0;

-- Exercise 28: Trace recent fulfillments through their stock and financial sources.
SELECT TOP (200) f.Id FulfillmentId,f.OrderId,f.IsReturn,f.ReversesId,d.Id StockDocumentId,d.Status StockStatus,
i.Id InvoiceId,i.IsCredit,i.Amount,i.CostAmount
FROM orders.Fulfillments f JOIN inventory.Documents d ON d.Id=f.StockDocumentId
LEFT JOIN finance.Invoices i ON i.StockDocumentId=f.StockDocumentId ORDER BY f.Id DESC;

-- Exercise 29: Find fulfillments missing a finance invoice; success means no rows.
SELECT f.Id,f.OrderId,f.StockDocumentId FROM orders.Fulfillments f
WHERE NOT EXISTS(SELECT 1 FROM finance.Invoices i WHERE i.StockDocumentId=f.StockDocumentId);

-- Exercise 30: Find multiple fulfillment requests with the same GUID; success means no rows.
SELECT RequestId,COUNT_BIG(*) Copies FROM orders.Fulfillments GROUP BY RequestId HAVING COUNT_BIG(*)>1;

-- Exercise 31: List partner order history without assuming all partners are customers.
SELECT TOP (200) p.Code,p.Kind PartnerKind,p.Name,o.Id OrderId,o.Kind OrderKind,o.Status,o.CreatedAtUtc
FROM catalog.BusinessPartners p JOIN orders.Orders o ON o.PartnerId=p.Id ORDER BY o.CreatedAtUtc DESC,o.Id DESC;

-- Exercise 32: Rank customers by actual net invoiced revenue in the interval.
SELECT TOP (20) p.Code,p.Name,SUM(CASE WHEN i.IsCredit=1 THEN -i.Amount ELSE i.Amount END) NetRevenue
FROM finance.Invoices i JOIN catalog.BusinessPartners p ON p.Id=i.PartnerId
WHERE i.Direction=1 AND i.IssuedAtUtc>=@From AND i.IssuedAtUtc<@To GROUP BY p.Code,p.Name ORDER BY NetRevenue DESC,p.Code;

-- Exercise 33: Calculate revenue, cost and gross profit from posted finance sources.
SELECT COALESCE(SUM(CASE WHEN IsCredit=1 THEN -Amount ELSE Amount END),0) Revenue,
COALESCE(SUM(CASE WHEN IsCredit=1 THEN -CostAmount ELSE CostAmount END),0) CostOfGoods,
COALESCE(SUM(CASE WHEN IsCredit=1 THEN -(Amount-CostAmount) ELSE Amount-CostAmount END),0) GrossProfit
FROM finance.Invoices WHERE Direction=1 AND IssuedAtUtc>=@From AND IssuedAtUtc<@To;

-- Exercise 34: Calculate net purchasing value in the interval.
SELECT COALESCE(SUM(CASE WHEN IsCredit=1 THEN -Amount ELSE Amount END),0) NetPurchased
FROM finance.Invoices WHERE Direction=2 AND IssuedAtUtc>=@From AND IssuedAtUtc<@To;

-- Exercise 35: Produce a UTC daily revenue series for days that contain postings.
SELECT CAST(IssuedAtUtc AS date) UtcDay,SUM(CASE WHEN IsCredit=1 THEN -Amount ELSE Amount END) Revenue
FROM finance.Invoices WHERE Direction=1 AND IssuedAtUtc>=@From AND IssuedAtUtc<@To
GROUP BY CAST(IssuedAtUtc AS date) ORDER BY UtcDay;

-- Exercise 36: Get invoice open amounts as of the cutoff, including credits separately.
SELECT TOP (200) i.Id,i.Direction,i.IsCredit,i.Amount,COALESCE(a.Paid,0) Allocated,i.Amount-COALESCE(a.Paid,0) OpenAmount
FROM finance.Invoices i OUTER APPLY(SELECT SUM(a.Amount) Paid FROM finance.Allocations a
JOIN finance.Payments p ON p.Id=a.PaymentId WHERE a.InvoiceId=i.Id AND p.PostedAtUtc<=@AsOf) a
WHERE i.IssuedAtUtc<=@AsOf ORDER BY i.Id DESC;

-- Exercise 37: Find overallocated invoices; success means no rows.
SELECT i.Id,i.Amount,SUM(a.Amount) Allocated FROM finance.Invoices i JOIN finance.Allocations a ON a.InvoiceId=i.Id
GROUP BY i.Id,i.Amount HAVING SUM(a.Amount)>i.Amount;

-- Exercise 38: Reconcile each payment to its allocations; success means no rows.
SELECT p.Id,p.Amount,COALESCE(SUM(a.Amount),0) Allocated FROM finance.Payments p
LEFT JOIN finance.Allocations a ON a.PaymentId=p.Id GROUP BY p.Id,p.Amount HAVING p.Amount<>COALESCE(SUM(a.Amount),0);

-- Exercise 39: Find invalid cross-partner/direction/refund allocations; success means no rows.
SELECT a.PaymentId,a.InvoiceId,p.PartnerId PaymentPartner,i.PartnerId InvoicePartner
FROM finance.Allocations a JOIN finance.Payments p ON p.Id=a.PaymentId JOIN finance.Invoices i ON i.Id=a.InvoiceId
WHERE p.PartnerId<>i.PartnerId OR p.Direction<>i.Direction OR p.IsRefund<>i.IsCredit;

-- Exercise 40: Calculate signed open receivable/payable balances per partner.
SELECT i.PartnerId,i.Direction,SUM((i.Amount-COALESCE(a.Paid,0))*CASE WHEN i.IsCredit=1 THEN -1 ELSE 1 END) Balance
FROM finance.Invoices i OUTER APPLY(SELECT SUM(a.Amount) Paid FROM finance.Allocations a
JOIN finance.Payments p ON p.Id=a.PaymentId WHERE a.InvoiceId=i.Id AND p.PostedAtUtc<=@AsOf) a
WHERE i.IssuedAtUtc<=@AsOf GROUP BY i.PartnerId,i.Direction;

-- Exercise 41: Bucket signed open balances by due-date age.
WITH OpenItems AS(SELECT i.PartnerId,i.Direction,DATEDIFF(day,i.DueAtUtc,@AsOf) Age,
(i.Amount-COALESCE(a.Paid,0))*CASE WHEN i.IsCredit=1 THEN -1 ELSE 1 END Balance
FROM finance.Invoices i OUTER APPLY(SELECT SUM(a.Amount) Paid FROM finance.Allocations a JOIN finance.Payments p ON p.Id=a.PaymentId
WHERE a.InvoiceId=i.Id AND p.PostedAtUtc<=@AsOf) a WHERE i.IssuedAtUtc<=@AsOf)
SELECT PartnerId,Direction,SUM(CASE WHEN Age<=0 THEN Balance ELSE 0 END) [Current],
SUM(CASE WHEN Age BETWEEN 1 AND 30 THEN Balance ELSE 0 END) Days1To30,
SUM(CASE WHEN Age BETWEEN 31 AND 60 THEN Balance ELSE 0 END) Days31To60,
SUM(CASE WHEN Age BETWEEN 61 AND 90 THEN Balance ELSE 0 END) Days61To90,
SUM(CASE WHEN Age>90 THEN Balance ELSE 0 END) DaysOver90 FROM OpenItems GROUP BY PartnerId,Direction;

-- Exercise 42: Summarize cash/payment movement separately from revenue.
SELECT Direction,IsRefund,COUNT_BIG(*) Payments,SUM(Amount) Amount FROM finance.Payments
WHERE PostedAtUtc>=@From AND PostedAtUtc<@To GROUP BY Direction,IsRefund;

-- Exercise 43: Find duplicate payment request IDs; success means no rows.
SELECT RequestId,COUNT_BIG(*) Copies FROM finance.Payments GROUP BY RequestId HAVING COUNT_BIG(*)>1;

-- Exercise 44: Reconcile signed invoice minus cash totals to signed outstanding at the cutoff.
WITH I AS(SELECT Direction,SUM(Amount*CASE WHEN IsCredit=1 THEN -1 ELSE 1 END) NetInvoices FROM finance.Invoices WHERE IssuedAtUtc<=@AsOf GROUP BY Direction),
P AS(SELECT Direction,SUM(Amount*CASE WHEN IsRefund=1 THEN -1 ELSE 1 END) NetPaid FROM finance.Payments WHERE PostedAtUtc<=@AsOf GROUP BY Direction),
O AS(SELECT i.Direction,SUM((i.Amount-COALESCE(a.Paid,0))*CASE WHEN i.IsCredit=1 THEN -1 ELSE 1 END) Outstanding
FROM finance.Invoices i OUTER APPLY(SELECT SUM(a.Amount) Paid FROM finance.Allocations a JOIN finance.Payments p ON p.Id=a.PaymentId
WHERE a.InvoiceId=i.Id AND p.PostedAtUtc<=@AsOf) a WHERE i.IssuedAtUtc<=@AsOf GROUP BY i.Direction)
SELECT i.Direction,i.NetInvoices,COALESCE(p.NetPaid,0) NetPaid,o.Outstanding,
i.NetInvoices-COALESCE(p.NetPaid,0)-o.Outstanding Difference FROM I i LEFT JOIN P p ON p.Direction=i.Direction JOIN O o ON o.Direction=i.Direction;

-- Exercise 45: Check fulfillment/invoice amount agreement; success means no rows.
SELECT f.Id,f.Amount FulfillmentAmount,i.Amount InvoiceAmount FROM orders.Fulfillments f
JOIN finance.Invoices i ON i.StockDocumentId=f.StockDocumentId WHERE f.Amount<>i.Amount;

-- Exercise 46: Inspect order audit timeline without returning credentials or contact data.
SELECT TOP (200) Id,TimestampUtc,Action,Entity,EntityId,Result,CorrelationId
FROM audit.AuditLogs WHERE Entity='Order' AND TimestampUtc>=@From AND TimestampUtc<@To ORDER BY TimestampUtc DESC,Id DESC;

-- Exercise 47: Count action outcomes by day.
SELECT CAST(TimestampUtc AS date) UtcDay,Action,Result,COUNT_BIG(*) Events FROM audit.AuditLogs
WHERE TimestampUtc>=@From AND TimestampUtc<@To GROUP BY CAST(TimestampUtc AS date),Action,Result ORDER BY UtcDay,Action,Result;

-- Exercise 48: Inspect applied migration history (ordered schema provenance).
SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

-- Exercise 49: Inspect existing indexes for the hot stock position and order tables.
SELECT s.name SchemaName,t.name TableName,i.name IndexName,i.is_unique,c.name ColumnName,ic.key_ordinal,ic.is_descending_key,ic.is_included_column
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.indexes i ON i.object_id=t.object_id
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE (s.name='inventory' AND t.name IN('Balances','Movements')) OR (s.name='orders' AND t.name='Orders')
ORDER BY s.name,t.name,i.name,ic.key_ordinal,ic.index_column_id;

-- Exercise 50: Demonstrate a bounded keyset page on movement IDs, with no unsafe dynamic SQL.
DECLARE @AfterMovementId bigint=0;
SELECT TOP (20) Id,DocumentId,WarehouseId,ProductId,QuantityDelta,PostedAtUtc
FROM inventory.Movements WHERE Id>@AfterMovementId ORDER BY Id;
