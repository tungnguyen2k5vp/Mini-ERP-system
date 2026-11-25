using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Domain.Catalog;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Catalog;

public sealed class CatalogSeed(ErpDbContext db, TimeProvider clock)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (await db.AuditEntries.AnyAsync(x => x.Action == "SeedCatalog" && x.Entity == "Demo", ct))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var groups = new[] { "Giấy in", "Bút viết", "Sổ tay", "Bìa hồ sơ", "Vật tư in", "Thiết bị văn phòng", "Phụ kiện bàn làm việc", "Đóng gói" };
        var categories = groups.Select((name, i) => Lookup(LookupKind.Category, $"NH-{i + 1:00}", name)).ToArray();
        var units = new[] { Lookup(LookupKind.Unit, "CAI", "Cái"), Lookup(LookupKind.Unit, "HOP", "Hộp"),
            Lookup(LookupKind.Unit, "RAM", "Ram"), Lookup(LookupKind.Unit, "BO", "Bộ") };
        db.Lookups.AddRange(categories.Concat(units));
        db.Lookups.AddRange(
            Lookup(LookupKind.Warehouse, "KHO-HN", "Kho Hà Nội"),
            Lookup(LookupKind.Warehouse, "KHO-HCM", "Kho Hồ Chí Minh"),
            Lookup(LookupKind.Warehouse, "KHO-DN", "Kho Đà Nẵng"),
            Lookup(LookupKind.PaymentMethod, "CK", "Chuyển khoản"),
            Lookup(LookupKind.PaymentMethod, "TM", "Tiền mặt"));

        for (var i = 0; i < categories.Length; i++)
        {
            for (var j = 1; j <= 13; j++)
            {
                var cost = 12_000 + (i * 37_000) + (j * 5_500);
                db.Products.Add(new Product
                {
                    Sku = $"VP-{i + 1:00}-{j:00}", Name = $"{groups[i]} · Mẫu {j:00}",
                    CategoryId = categories[i].Id, UnitId = units[i == 0 ? 2 : i % units.Length].Id,
                    Description = "Mặt hàng minh họa của doanh nghiệp phân phối.",
                    PurchasePrice = cost, SalePrice = cost * 1.24m, MinimumStock = 5 + j % 9,
                    CreatedAtUtc = now, UpdatedAtUtc = now
                });
            }
        }
        db.Partners.AddRange(Enumerable.Range(1, 30).Select(i => new BusinessPartner
        {
            Kind = PartnerKind.Customer, Code = $"KH-{i:000}", Name = $"Cửa hàng Mộc Lan {i:00}",
            Email = $"customer{i:00}@minierp.example", CreditLimit = 5_000_000 + i * 500_000, PaymentTermsDays = 30
        }));
        db.Partners.AddRange(Enumerable.Range(1, 15).Select(i => new BusinessPartner
        {
            Kind = PartnerKind.Supplier, Code = $"NCC-{i:000}", Name = $"Nhà cung ứng Hướng Dương {i:00}",
            Email = $"supplier{i:00}@minierp.example", PaymentTermsDays = (i % 3 + 1) * 15
        }));

        db.AuditEntries.Add(AuditEntry.Create(new RequestActor(null, "seed-catalog"), now, "SeedCatalog", "Demo", null));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static CatalogLookup Lookup(LookupKind kind, string code, string name) => new() { Kind = kind, Code = code, Name = name };
}
