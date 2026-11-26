using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Orders;

public sealed class OrderDemoSeed(ErpDbContext db, TimeProvider clock)
{
    private static readonly RequestActor Actor = new(null, "seed-order-history");
    private static readonly Guid WarehouseId = new("fb880000-a43b-4b17-9dfa-000000000001");

    public async Task SeedAsync(CancellationToken ct)
    {
        // Hold a session lock across the individual, independently resumable business transactions.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @r int;
                EXEC @r = sys.sp_getapplock @Resource='MiniErp.DemoOrders', @LockMode='Exclusive',
                    @LockOwner='Session', @LockTimeout=10000;
                IF @r < 0 THROW 51000, 'A history seed is already running.', 1;
                """, ct);
            if (await db.AuditEntries.AnyAsync(x => x.Action == "SeedOrderHistory", ct)) return;
            await GenerateAsync(ct);
            db.AuditEntries.Add(AuditEntry.Create(Actor, clock.GetUtcNow().UtcDateTime, "SeedOrderHistory", "Demo", null));
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task GenerateAsync(CancellationToken ct)
    {
        // A dedicated warehouse keeps historical movements apart from all existing stock positions.
        if (!await db.Lookups.AnyAsync(x => x.Id == WarehouseId, ct))
        {
            db.Lookups.Add(new CatalogLookup { Id = WarehouseId, Kind = LookupKind.Warehouse,
                Code = "DEMO-LS", Name = "Kho mô phỏng phân phối" });
            await db.SaveChangesAsync(ct);
        }
        var end = clock.GetUtcNow().UtcDateTime.Date.AddDays(-1);
        var firstKey = Key(0, 0, 0);
        var first = await db.Orders.Where(x => x.RequestId == firstKey).Select(x => (DateTime?)x.CreatedAtUtc).SingleOrDefaultAsync(ct);
        var start = DateTime.SpecifyKind(first ?? end.AddMonths(-6), DateTimeKind.Utc);
        var days = (start.AddMonths(6) - start).Days;
        var products = await db.Products.AsNoTracking().Where(x => x.Sku.StartsWith("VP-")).OrderBy(x => x.Sku).Take(24).ToArrayAsync(ct);
        var suppliers = await db.Partners.Where(x => x.Kind == PartnerKind.Supplier && x.Code.StartsWith("NCC-")).OrderBy(x => x.Code).Select(x => x.Id).ToArrayAsync(ct);
        var customers = await db.Partners.Where(x => x.Kind == PartnerKind.Customer && x.Code.StartsWith("KH-")).OrderBy(x => x.Code).Select(x => x.Id).ToArrayAsync(ct);
        var method = await db.Lookups.Where(x => x.Kind == LookupKind.PaymentMethod && x.Code == "CK").Select(x => x.Id).SingleAsync(ct);
        if (products.Length == 0 || suppliers.Length == 0 || customers.Length == 0)
            throw new InvalidOperationException("Initialize the demo catalog before seeding commercial history.");

        for (var batch = 0; batch < 100; batch++)
        {
            var product = products[batch % products.Length];
            for (var step = 0; step < 3; step++)
            {
                var purchase = step == 0;
                var partner = purchase ? suppliers[batch % suppliers.Length] : customers[(batch * 2 + step - 1) % customers.Length];
                var instant = start.AddDays(batch * days / 99).AddHours(step);
                var demoClock = new DemoClock(instant);
                var finance = new FinanceService(db, demoClock);
                var orders = new OrderService(db, new StockService(db, demoClock), finance, demoClock);
                var quantity = purchase ? 32 + batch % 16 : 5 + (batch + step) % 9;
                var input = new OrderInput(Key(batch, step, 0), purchase ? OrderKind.Purchase : OrderKind.Sales,
                    partner, WarehouseId, "Lịch sử mô phỏng phân phối", [new(product.Id, quantity, purchase ? product.PurchasePrice : product.SalePrice)]);
                var order = await orders.CreateAsync(input, Actor, ct);
                if (order.Order.Status == OrderStatus.Draft)
                    order = await orders.TransitionAsync(order.Order.Id, "submit", new(order.Order.Version), Actor, ct);
                if (order.Order.Status == OrderStatus.PendingApproval)
                    order = await orders.TransitionAsync(order.Order.Id, "approve", new(order.Order.Version), Actor, ct);
                // Replaying any individual step uses its original request key after a process interruption.
                if (batch % 5 == 0)
                    await orders.FulfillAsync(order.Order.Id, new(Key(batch, step, 1), false, [new(product.Id, 2)]), Actor, ct);
                order = await orders.FulfillAsync(order.Order.Id, new(Key(batch, step, 2), false,
                    [new(product.Id, quantity - (batch % 5 == 0 ? 2 : 0))]), Actor, ct);
                if (batch % 11 == 0 && step == 2)
                    await orders.FulfillAsync(order.Order.Id, new(Key(batch, step, 3), true, [new(product.Id, 1)]), Actor, ct);
                if ((batch + step) % 3 != 0)
                {
                    var documentId = order.Fulfillments.First(f => !f.IsReturn).StockDocumentId;
                    var invoice = await db.Set<FinanceInvoice>().AsNoTracking().SingleAsync(x => x.StockDocumentId == documentId, ct);
                    var amount = Money.Round(invoice.Amount / 2);
                    if (amount > 0)
                        await finance.PayAsync(new PaymentInput(Key(batch, step, 4), partner, invoice.Direction, false,
                            method, amount, "CK-DEMO", [new(invoice.Id, amount)]), Actor, ct);
                }
                db.ChangeTracker.Clear();
            }
        }
    }

    private static Guid Key(int batch, int step, int action) => new($"fb881000-a43b-4b17-9dfa-{batch:D6}{step:D3}{action:D3}");

    private sealed class DemoClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }
}
