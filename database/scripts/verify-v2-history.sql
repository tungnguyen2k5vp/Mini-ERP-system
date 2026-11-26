-- Run on a quiet V1 rehearsal database, then repeat immediately after the additive V2 migration.
-- The projection deliberately contains only columns common to both versions.
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

SELECT Name, Rows, CONVERT(varchar(64), HASHBYTES('SHA2_256', Records), 2) AS HistorySha256
FROM (VALUES
    ('Stock documents', (SELECT COUNT_BIG(*) FROM inventory.Documents),
        (SELECT * FROM inventory.Documents ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Stock lines', (SELECT COUNT_BIG(*) FROM inventory.DocumentLines),
        (SELECT DocumentId,ProductId,Sku,ProductName,Quantity,UnitCost,CountVersion FROM inventory.DocumentLines
         ORDER BY DocumentId,ProductId FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Stock movements', (SELECT COUNT_BIG(*) FROM inventory.Movements),
        (SELECT * FROM inventory.Movements ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Stock balances', (SELECT COUNT_BIG(*) FROM inventory.Balances),
        (SELECT * FROM inventory.Balances ORDER BY WarehouseId,ProductId FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Trade orders', (SELECT COUNT_BIG(*) FROM orders.Orders),
        (SELECT * FROM orders.Orders ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Order lines', (SELECT COUNT_BIG(*) FROM orders.OrderLines),
        (SELECT * FROM orders.OrderLines ORDER BY OrderId,ProductId FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Fulfillments', (SELECT COUNT_BIG(*) FROM orders.Fulfillments),
        (SELECT * FROM orders.Fulfillments ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Invoices', (SELECT COUNT_BIG(*) FROM finance.Invoices),
        (SELECT * FROM finance.Invoices ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Payments', (SELECT COUNT_BIG(*) FROM finance.Payments),
        (SELECT * FROM finance.Payments ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES)),
    ('Allocations', (SELECT COUNT_BIG(*) FROM finance.Allocations),
        (SELECT * FROM finance.Allocations ORDER BY PaymentId,InvoiceId FOR JSON PATH, INCLUDE_NULL_VALUES))
) Sources(Name,Rows,Records) ORDER BY Name;

COMMIT;
