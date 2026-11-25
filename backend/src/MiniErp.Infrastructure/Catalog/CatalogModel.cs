using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Catalog;

namespace MiniErp.Infrastructure.Catalog;

internal static class CatalogModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<CatalogLookup>(lookup =>
        {
            lookup.ToTable("Lookups", "catalog", t => t.HasCheckConstraint("CK_Lookups_Kind", "[Kind] IN (1, 2, 3, 4)"));
            lookup.HasAlternateKey(x => new { x.Kind, x.Id });
            lookup.HasIndex(x => new { x.Kind, x.Code }).IsUnique();
            lookup.Property(x => x.Code).HasMaxLength(32).IsUnicode(false).IsRequired();
            lookup.Property(x => x.Name).HasMaxLength(160).IsRequired();
            lookup.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<Product>(product =>
        {
            product.ToTable("Products", "catalog", t =>
            {
                t.HasCheckConstraint("CK_Products_Prices", "[PurchasePrice] >= 0 AND [SalePrice] >= 0 AND [MinimumStock] >= 0");
                t.HasCheckConstraint("CK_Products_LookupKinds", "[CategoryKind] = 1 AND [UnitKind] = 2");
            });
            product.HasIndex(x => x.Sku).IsUnique();
            product.Property(x => x.Sku).HasMaxLength(32).IsUnicode(false).IsRequired();
            product.Property(x => x.Name).HasMaxLength(160).IsRequired();
            product.Property(x => x.Description).HasMaxLength(500);
            product.Property(x => x.ImageUrl).HasMaxLength(500);
            product.Property(x => x.PurchasePrice).HasPrecision(19, 4);
            product.Property(x => x.SalePrice).HasPrecision(19, 4);
            product.Property(x => x.MinimumStock).HasPrecision(19, 4);
            product.Property(x => x.Version).IsRowVersion();
            product.HasOne(x => x.Category).WithMany().HasForeignKey(x => new { Kind = x.CategoryKind, Id = x.CategoryId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            product.HasOne(x => x.Unit).WithMany().HasForeignKey(x => new { Kind = x.UnitKind, Id = x.UnitId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BusinessPartner>(partner =>
        {
            partner.ToTable("BusinessPartners", "catalog", t =>
            {
                t.HasCheckConstraint("CK_BusinessPartners_Kind", "[Kind] IN (1, 2)");
                t.HasCheckConstraint("CK_BusinessPartners_Finance",
                    "[CreditLimit] >= 0 AND [PaymentTermsDays] BETWEEN 0 AND 365 AND ([Kind] = 1 OR [CreditLimit] = 0)");
            });
            partner.HasIndex(x => new { x.Kind, x.Code }).IsUnique();
            partner.Property(x => x.Code).HasMaxLength(32).IsUnicode(false).IsRequired();
            partner.Property(x => x.Name).HasMaxLength(160).IsRequired();
            partner.Property(x => x.Phone).HasMaxLength(32);
            partner.Property(x => x.Email).HasMaxLength(254);
            partner.Property(x => x.Address).HasMaxLength(250);
            partner.Property(x => x.TaxCode).HasMaxLength(32);
            partner.Property(x => x.CreditLimit).HasPrecision(19, 4);
            partner.Property(x => x.Version).IsRowVersion();
        });
    }
}
