using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Inventory;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Inventory;

public sealed class StockDemoSeed(ErpDbContext db, StockService stock)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var actor = new RequestActor(null, "seed-inventory");
        var warehouses = new[] { "KHO-HN", "KHO-HCM", "KHO-DN" };
        for (var i = 0; i < warehouses.Length; i++)
        {
            var requestId = new Guid($"98230000-edd1-4276-bc47-00000000000{i + 1}");
            var id = await db.StockDocuments.Where(x => x.RequestId == requestId)
                .Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
            if (id is null)
            {
                var code = warehouses[i];
                var warehouseId = await db.Lookups.Where(x => x.Kind == LookupKind.Warehouse && x.Code == code)
                    .Select(x => x.Id).SingleAsync(ct);
                var products = await db.Products.Where(x => x.IsActive && x.Sku.StartsWith("VP-"))
                    .OrderBy(x => x.Sku).Skip(i * 16).Take(32).Select(x => new { x.Id, x.PurchasePrice }).ToArrayAsync(ct);
                var input = new StockDocumentInput(requestId, StockKind.Receipt, warehouseId, null, "Nhập tồn đầu kỳ minh họa",
                    products.Select((x, n) => new StockLineInput(x.Id, n % 6 == 0 ? 3 : 24 + n * 4, x.PurchasePrice)).ToArray());
                id = (await stock.CreateAsync(input, actor, ct)).Document.Id;
            }

            if (await db.StockDocuments.AnyAsync(x => x.Id == id && x.Status == StockStatus.Draft, ct))
            {
                await stock.PostAsync(id.Value, actor, ct);
            }
        }
    }
}
