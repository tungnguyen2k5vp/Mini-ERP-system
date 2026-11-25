using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Inventory;

public sealed class InventoryReport(ErpDbContext db)
{
    public async Task<Page<StockBalanceRow>> GetPageAsync(Guid? warehouseId, Guid? productId, string? search,
        bool lowOnly, int page, int size, CancellationToken ct)
    {
        await using var connection = new SqlConnection(db.Database.GetConnectionString());
        var command = new CommandDefinition("reporting.usp_GetInventoryReport",
            new { WarehouseId = warehouseId, ProductId = productId, Search = search?.Trim(), LowOnly = lowOnly,
                Offset = (page - 1) * size, PageSize = size },
            commandType: CommandType.StoredProcedure, cancellationToken: ct);
        using var results = await connection.QueryMultipleAsync(command);
        var total = await results.ReadSingleAsync<int>();
        var items = (await results.ReadAsync<StockBalanceRow>()).AsList();
        return new Page<StockBalanceRow>(items, page, size, total);
    }
}
