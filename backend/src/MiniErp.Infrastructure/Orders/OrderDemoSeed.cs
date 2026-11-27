using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Finance;
using MiniErp.Application.Orders;
using MiniErp.Application.Inventory;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Orders;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Finance;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Orders;

public sealed class OrderDemoSeed(ErpDbContext db, TimeProvider clock)
{
    private static readonly RequestActor Actor = new(null, "seed-order-history");
    private static readonly Guid WarehouseId = new("fb880000-a43b-4b17-9dfa-000000000001");
    public static readonly Guid CreditDemoPartnerId = new("fb883000-a43b-4b17-9dfa-000000000001");
    public static readonly Guid CreditDemoWarehouseId = new("fb883000-a43b-4b17-9dfa-000000000002");

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
            if (!await db.AuditEntries.AnyAsync(x => x.Action == "SeedOrderHistory", ct))
            {
                await GenerateAsync(ct);
                db.AuditEntries.Add(AuditEntry.Create(Actor, clock.GetUtcNow().UtcDateTime, "SeedOrderHistory", "Demo", null));
                await db.SaveChangesAsync(ct);
            }
            if (!await db.AuditEntries.AnyAsync(x => x.Action == "SeedCreditCorrectionDemo", ct))
            {
                await GenerateCreditCorrectionAsync(ct);
                db.AuditEntries.Add(AuditEntry.Create(Actor, clock.GetUtcNow().UtcDateTime, "SeedCreditCorrectionDemo", "Demo", CreditDemoPartnerId.ToString()));
                await db.SaveChangesAsync(ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task GenerateCreditCorrectionAsync(CancellationToken ct)
    {
        const string customerName = "Công ty Phân phối Minh An";
        var warehouse = await db.Lookups.AsNoTracking().SingleOrDefaultAsync(x => x.Id == CreditDemoWarehouseId ||
            x.Kind == LookupKind.Warehouse && x.Code == "DEMO-TD", ct);
        if (warehouse is null)
        {
            db.Lookups.Add(new CatalogLookup { Id = CreditDemoWarehouseId, Kind = LookupKind.Warehouse, Code = "DEMO-TD", Name = "Kho mô phỏng tín dụng" });
            db.AuditEntries.Add(AuditEntry.Create(Actor, clock.GetUtcNow().UtcDateTime, "SeedCreditWarehouse", "Warehouse", CreditDemoWarehouseId.ToString()));
            await db.SaveChangesAsync(ct);
        }
        else if (warehouse.Id != CreditDemoWarehouseId || warehouse.Kind != LookupKind.Warehouse || warehouse.Code != "DEMO-TD" ||
            warehouse.Name != "Kho mô phỏng tín dụng" || !warehouse.IsActive ||
            !await db.AuditEntries.AnyAsync(x => x.Action == "SeedCreditWarehouse" && x.EntityId == CreditDemoWarehouseId.ToString(), ct))
            throw new InvalidOperationException("The dedicated credit warehouse is not an active seed-owned fixture.");
        var receiptKey = CreditKey(1);
        // A resumed scenario may only contain its own receipt and linked shipment. Never backfill a used position.
        if (await db.StockDocuments.AnyAsync(x => (x.WarehouseId == CreditDemoWarehouseId || x.DestinationId == CreditDemoWarehouseId) &&
            x.RequestId != receiptKey && x.RequestId != CreditKey(3), ct))
            throw new InvalidOperationException("The credit demo warehouse already contains unrelated documents; it cannot be backfilled.");
        if (!await db.StockDocuments.AnyAsync(x => x.RequestId == receiptKey && x.WarehouseId == CreditDemoWarehouseId, ct) &&
            await db.StockBalances.AnyAsync(x => x.WarehouseId == CreditDemoWarehouseId, ct))
            throw new InvalidOperationException("The credit demo warehouse must start without existing stock balances.");
        var partner = await db.Partners.AsNoTracking().SingleOrDefaultAsync(x => x.Id == CreditDemoPartnerId || x.Kind == PartnerKind.Customer && x.Code == "DEMO-TD", ct);
        if (partner is null)
        {
            // The fixed ID is a seed fixture identity; normal customer creation still uses CatalogService.
            db.Partners.Add(new BusinessPartner { Id = CreditDemoPartnerId, Kind = PartnerKind.Customer, Code = "DEMO-TD",
                Name = customerName, CreditLimit = 500_000m, PaymentTermsDays = 30 });
            db.AuditEntries.Add(AuditEntry.Create(Actor, clock.GetUtcNow().UtcDateTime, "SeedCreditCustomer", "Customer", CreditDemoPartnerId.ToString()));
            await db.SaveChangesAsync(ct);
        }
        else if (partner.Id != CreditDemoPartnerId || partner.Kind != PartnerKind.Customer || partner.Code != "DEMO-TD" ||
            partner.Name != customerName || partner.CreditLimit != 500_000m || partner.PaymentTermsDays != 30 || !partner.IsActive)
            throw new InvalidOperationException("The credit demo customer has changed; existing business records will not be overwritten.");

        var product = await db.Products.AsNoTracking().Where(x => x.Sku == "VP-01-01" && x.IsActive).Select(x => x.Id).SingleAsync(ct);
        var method = await db.Lookups.Where(x => x.Kind == LookupKind.PaymentMethod && x.Code == "CK" && x.IsActive).Select(x => x.Id).SingleAsync(ct);
        var anchor = await db.StockDocuments.Where(x => x.RequestId == receiptKey).Select(x => (DateTime?)x.CreatedAtUtc).SingleOrDefaultAsync(ct)
            ?? clock.GetUtcNow().UtcDateTime.Date.AddDays(-35).AddHours(2);
        anchor = DateTime.SpecifyKind(anchor, DateTimeKind.Utc);
        var stock = new StockService(db, new DemoClock(anchor));
        // Fixed quantities, cost and selling price keep semantic retries stable after catalog price changes.
        var receipt = await stock.CreateAsync(new StockDocumentInput(receiptKey, StockKind.Receipt, CreditDemoWarehouseId, null,
            "Bổ sung hàng cho kịch bản hạn mức Minh An", [new(product, 10m, 45_000m)]), Actor, ct);
        if (receipt.Document.Status == StockStatus.Draft)
            await stock.PostAsync(receipt.Document.Id, Actor, ct, version: receipt.Document.Version);
        else if (receipt.Document.Status != StockStatus.Posted)
            throw new InvalidOperationException("The credit demo receipt is no longer posted.");

        var orderClock = new DemoClock(anchor.AddHours(1));
        var orders = new OrderService(db, new StockService(db, orderClock), new FinanceService(db, orderClock), orderClock);
        var order = await orders.CreateAsync(new OrderInput(CreditKey(2), OrderKind.Sales, CreditDemoPartnerId, CreditDemoWarehouseId,
            "Minh An: duyệt vượt hạn mức có lý do, giao một phần và sửa phiếu thu", [new(product, 10m, 60_000m)]), Actor, ct);
        if (order.Order.Status == OrderStatus.Draft)
            order = await orders.TransitionAsync(order.Order.Id, "submit", new(order.Order.Version), Actor, ct);
        if (order.Order.Status == OrderStatus.PendingApproval)
            order = await orders.TransitionAsync(order.Order.Id, "approve", new(order.Order.Version, "Demo có kiểm soát: duyệt 600.000đ trên hạn mức 500.000đ"),
                Actor, ct, canSelfApprove: true, canApproveAboveLimit: true, canOverrideCreditLimit: true);
        var shipmentClock = new DemoClock(anchor.AddHours(2));
        orders = new OrderService(db, new StockService(db, shipmentClock), new FinanceService(db, shipmentClock), shipmentClock);
        order = await orders.FulfillAsync(order.Order.Id, new(CreditKey(3), false, [new(product, 8m)]), Actor, ct);
        var shipment = order.Fulfillments.Single(x => !x.IsReturn && x.ReversesId is null);
        var invoice = await db.Set<FinanceInvoice>().AsNoTracking().SingleAsync(x => x.StockDocumentId == shipment.StockDocumentId, ct);
        var finance = new FinanceService(db, new DemoClock(anchor.AddHours(3)));
        var wrong = await finance.PayAsync(new PaymentInput(CreditKey(4), CreditDemoPartnerId, FinanceDirection.Receivable, false,
            method, 180_000m, "DEMO-TD: ghi nhầm số tiền thu", [new(invoice.Id, 180_000m)]), Actor, ct);
        finance = new FinanceService(db, new DemoClock(anchor.AddDays(1).AddHours(1)));
        await finance.ReversePaymentAsync(wrong.Id, new(CreditKey(5), "Đảo toàn bộ phiếu thu nhầm 180.000đ; số thu đúng là 120.000đ"), Actor, ct);
        finance = new FinanceService(db, new DemoClock(anchor.AddDays(1).AddHours(2)));
        await finance.PayAsync(new PaymentInput(CreditKey(6), CreditDemoPartnerId, FinanceDirection.Receivable, false,
            method, 120_000m, "DEMO-TD: thu đúng sau khi đảo phiếu nhầm", [new(invoice.Id, 120_000m)]), Actor, ct);
        var exposure = await CustomerCredit.ForPartnerAsync(db, CreditDemoPartnerId, ct);
        if (exposure.CreditLimit != 500_000m || exposure.Outstanding != 360_000m || exposure.Unbilled != 120_000m || exposure.Used != 480_000m)
            throw new InvalidOperationException("The credit demo no longer matches its controlled 360000 debt + 120000 commitment scenario.");
        // A runnable seed invariant also guards historical stock ordering in CI/fresh-install rehearsals.
        var movements = await db.StockMovements.AsNoTracking().Where(x => x.WarehouseId == CreditDemoWarehouseId)
            .OrderBy(x => x.PostedAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
        var balance = await db.StockBalances.AsNoTracking().SingleAsync(x => x.WarehouseId == CreditDemoWarehouseId && x.ProductId == product, ct);
        if (movements.Length != 2 || movements.Any(x => x.ProductId != product || x.UnitCost != 45_000m) ||
            movements[0].QuantityDelta != 10m || movements[0].BalanceAfter != 10m || movements[0].PreviousAverageCost != 0m ||
            movements[1].QuantityDelta != -8m || movements[1].BalanceAfter != 2m || movements[1].PreviousAverageCost != 45_000m ||
            balance.OnHand != 2m || balance.Reserved != 2m || balance.AverageCost != 45_000m)
            throw new InvalidOperationException("The credit demo chronological stock balances or moving average cost are inconsistent.");
    }

    private static Guid CreditKey(int step) => new($"fb883100-a43b-4b17-9dfa-{step:D12}");

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
                    order = await orders.TransitionAsync(order.Order.Id, "submit", new(order.Order.Version), Actor, ct, canSelfApprove: true, canApproveAboveLimit: true, canOverrideCreditLimit: true);
                if (order.Order.Status == OrderStatus.PendingApproval)
                    // Historical fixtures deliberately retain their original activity even above current customer limits.
                    order = await orders.TransitionAsync(order.Order.Id, "approve", new(order.Order.Version, "Historical demo seed"), Actor, ct, canSelfApprove: true, canApproveAboveLimit: true, canOverrideCreditLimit: true);
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
