using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.Application;
using MiniErp.Application.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Inventory;

public sealed class InventoryReport(ErpDbContext db)
{
    public async Task<Page<StockReconciliationRow>> ReconciliationAsync(Guid? warehouseId, Guid? productId,
        int page, int size, CancellationToken ct)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = db.Database.CurrentTransaction!.GetDbTransaction();
        if (current.IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Stock reconciliation requires a serializable read transaction.");
        var command = new CommandDefinition(ReconciliationQuery + "SELECT * INTO #StockDiscrepancies FROM Discrepancies;" +
            "SELECT COUNT(*) FROM #StockDiscrepancies; SELECT * FROM #StockDiscrepancies ORDER BY WarehouseId,ProductId OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY; DROP TABLE #StockDiscrepancies; DROP TABLE #StockActual;",
            new { WarehouseId = warehouseId, ProductId = productId, Offset = (page - 1) * size, PageSize = size },
            transaction: current, cancellationToken: ct);
        int total;
        List<StockReconciliationRow> items;
        using (var results = await db.Database.GetDbConnection().QueryMultipleAsync(command))
        {
            total = await results.ReadSingleAsync<int>();
            items = (await results.ReadAsync<StockReconciliationRow>()).AsList();
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(items, page, size, total);
    }

    private const string ReconciliationQuery = """
        WITH LinkedLines AS (
          SELECT o.Id OrderId,o.WarehouseId,l.ProductId,
            CASE WHEN f.ReversesId IS NULL AND f.IsReturn=0 THEN l.Quantity
                 WHEN f.ReversesId IS NOT NULL AND original.IsReturn=0 THEN -l.Quantity ELSE 0 END Fulfilled,
            CASE WHEN f.ReversesId IS NULL AND f.IsReturn=1 THEN l.Quantity
                 WHEN f.ReversesId IS NOT NULL AND original.IsReturn=1 THEN -l.Quantity ELSE 0 END Returned,
            CASE WHEN d.Status NOT IN (2,4) OR d.WarehouseId<>o.WarehouseId
              OR (f.ReversesId IS NULL AND d.Kind<>CASE WHEN (o.Kind=1 AND f.IsReturn=0) OR (o.Kind=2 AND f.IsReturn=1) THEN 1 ELSE 2 END)
              OR (f.ReversesId IS NOT NULL AND (d.Kind<>6 OR d.ReversesId<>original.StockDocumentId OR original.OrderId<>f.OrderId OR original.IsReturn=f.IsReturn))
              OR m.Id IS NULL OR m.QuantityDelta<>l.Quantity*CASE WHEN (o.Kind=1 AND f.IsReturn=0) OR (o.Kind=2 AND f.IsReturn=1) THEN 1 ELSE -1 END
              THEN 1 ELSE 0 END BadSource
          FROM orders.Orders o JOIN orders.Fulfillments f ON f.OrderId=o.Id
          JOIN inventory.Documents d ON d.Id=f.StockDocumentId JOIN inventory.DocumentLines l ON l.DocumentId=d.Id
          LEFT JOIN orders.Fulfillments original ON original.Id=f.ReversesId
          LEFT JOIN inventory.Movements m ON m.DocumentId=d.Id AND m.WarehouseId=d.WarehouseId AND m.ProductId=l.ProductId
          WHERE (@WarehouseId IS NULL OR o.WarehouseId=@WarehouseId))
        SELECT OrderId,WarehouseId,ProductId,SUM(Fulfilled) Fulfilled,SUM(Returned) Returned,MAX(BadSource) BadSource
        INTO #StockActual FROM LinkedLines GROUP BY OrderId,WarehouseId,ProductId;
        CREATE UNIQUE CLUSTERED INDEX IX_StockActual ON #StockActual(OrderId,ProductId);
        WITH Ledger AS (
          SELECT WarehouseId,ProductId,SUM(QuantityDelta) Quantity,COUNT_BIG(*) Movements
          FROM inventory.Movements
          WHERE (@WarehouseId IS NULL OR WarehouseId=@WarehouseId) AND (@ProductId IS NULL OR ProductId=@ProductId)
          GROUP BY WarehouseId,ProductId),
        LineKeys AS (
          SELECT o.Id OrderId,o.WarehouseId,l.ProductId FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id
          WHERE (@WarehouseId IS NULL OR o.WarehouseId=@WarehouseId) AND (@ProductId IS NULL OR l.ProductId=@ProductId)
          UNION SELECT OrderId,WarehouseId,ProductId FROM #StockActual WHERE (@ProductId IS NULL OR ProductId=@ProductId)),
        OrderState AS (
          SELECT o.Id,MAX(CASE WHEN COALESCE(a.Fulfilled,0)>0 THEN 1 ELSE 0 END) AnyFulfilled,
            MIN(CASE WHEN COALESCE(a.Fulfilled,0)=l.Quantity THEN 1 ELSE 0 END) AllFulfilled
          FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id
          LEFT JOIN #StockActual a ON a.OrderId=o.Id AND a.ProductId=l.ProductId
          WHERE (@WarehouseId IS NULL OR o.WarehouseId=@WarehouseId)
          GROUP BY o.Id),
        ComparedLines AS (
          SELECT k.WarehouseId,k.ProductId,COALESCE(l.ReservedQuantity,0) SavedReservation,
            CASE WHEN o.Kind=2 AND o.Status IN (3,4) AND l.Quantity>COALESCE(a.Fulfilled,0)
                 THEN l.Quantity-COALESCE(a.Fulfilled,0) ELSE 0 END Commitment,
            CASE WHEN l.ProductId IS NULL OR COALESCE(a.BadSource,0)<>0
              OR l.FulfilledQuantity<>COALESCE(a.Fulfilled,0) OR l.ReturnedQuantity<>COALESCE(a.Returned,0)
              OR COALESCE(a.Fulfilled,0)<0 OR COALESCE(a.Fulfilled,0)>l.Quantity
              OR COALESCE(a.Returned,0)<0 OR COALESCE(a.Returned,0)>COALESCE(a.Fulfilled,0)
              OR l.ReservedQuantity<>CASE WHEN o.Kind=2 AND o.Status IN (3,4) AND l.Quantity>COALESCE(a.Fulfilled,0) THEN l.Quantity-COALESCE(a.Fulfilled,0) ELSE 0 END
              OR (o.Status IN (1,2,3) AND state.AnyFulfilled=1)
              OR (o.Status=4 AND (state.AnyFulfilled=0 OR state.AllFulfilled=1))
              OR (o.Status=5 AND state.AllFulfilled=0)
              THEN 1 ELSE 0 END Inconsistent
          FROM LineKeys k JOIN orders.Orders o ON o.Id=k.OrderId
          LEFT JOIN orders.OrderLines l ON l.OrderId=k.OrderId AND l.ProductId=k.ProductId
          LEFT JOIN #StockActual a ON a.OrderId=k.OrderId AND a.ProductId=k.ProductId
          LEFT JOIN OrderState state ON state.Id=k.OrderId),
        Reservations AS (
          SELECT WarehouseId,ProductId,SUM(SavedReservation) Reserved,SUM(Commitment) Commitment,SUM(Inconsistent) BadLines
          FROM ComparedLines GROUP BY WarehouseId,ProductId),
        PositionKeys AS (
          SELECT WarehouseId,ProductId FROM inventory.Balances
          WHERE (@WarehouseId IS NULL OR WarehouseId=@WarehouseId) AND (@ProductId IS NULL OR ProductId=@ProductId)
          UNION SELECT WarehouseId,ProductId FROM Ledger
          UNION SELECT WarehouseId,ProductId FROM Reservations),
        Discrepancies AS (
          SELECT k.WarehouseId,w.Name Warehouse,k.ProductId,p.Sku,COALESCE(b.OnHand,0) OnHand,
            COALESCE(ledger.Quantity,0) LedgerQuantity,COALESCE(b.Reserved,0) Reserved,
            COALESCE(r.Reserved,0) OrderReservations,COALESCE(r.Commitment,0) OpenCommitment,
            COALESCE(r.BadLines,0) InconsistentOrderLines,CAST(CASE WHEN b.ProductId IS NULL THEN 0 ELSE 1 END AS bit) HasBalance
          FROM PositionKeys k JOIN catalog.Lookups w ON w.Id=k.WarehouseId AND w.Kind=3
          JOIN catalog.Products p ON p.Id=k.ProductId
          LEFT JOIN inventory.Balances b ON b.WarehouseId=k.WarehouseId AND b.ProductId=k.ProductId
          LEFT JOIN Ledger ledger ON ledger.WarehouseId=k.WarehouseId AND ledger.ProductId=k.ProductId
          LEFT JOIN Reservations r ON r.WarehouseId=k.WarehouseId AND r.ProductId=k.ProductId
          WHERE COALESCE(b.OnHand,0)<>COALESCE(ledger.Quantity,0)
            OR COALESCE(b.Reserved,0)<>COALESCE(r.Reserved,0)
            OR COALESCE(b.Reserved,0)<>COALESCE(r.Commitment,0)
            OR COALESCE(r.BadLines,0)<>0
            OR (b.ProductId IS NULL AND COALESCE(ledger.Movements,0)>0))
        """;

    public async Task<Page<StockBalanceRow>> GetPageAsync(Guid? warehouseId, Guid? productId, string? search,
        bool lowOnly, int page, int size, CancellationToken ct)
    {
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var command = new CommandDefinition("reporting.usp_GetInventoryReport",
            new { WarehouseId = warehouseId, ProductId = productId, Search = search?.Trim(), LowOnly = lowOnly,
                Offset = (page - 1) * size, PageSize = size },
            transaction: transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct);
        int total;
        List<StockBalanceRow> items;
        using (var results = await connection.QueryMultipleAsync(command))
        {
            total = await results.ReadSingleAsync<int>();
            items = (await results.ReadAsync<StockBalanceRow>()).AsList();
        }
        await transaction.CommitAsync(ct);
        return new Page<StockBalanceRow>(items, page, size, total);
    }
}
