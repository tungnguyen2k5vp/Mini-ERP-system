using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;

namespace MiniErp.Infrastructure.Finance;

internal static class FinanceModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<FinanceInvoice>(e =>
        {
            e.ToTable("Invoices", "finance", t => {
                t.HasCheckConstraint("CK_Invoices_Amounts", "[Amount] >= 0 AND [CostAmount] >= 0");
                t.HasCheckConstraint("CK_Invoices_Direction", "[Direction] IN (1,2)");
                t.HasCheckConstraint("CK_Invoices_Dates", "[DueAtUtc] >= [IssuedAtUtc]");
            });
            e.HasIndex(x => x.StockDocumentId).IsUnique();
            e.HasIndex(x => new { x.Direction, x.PartnerId, x.DueAtUtc });
            e.Property(x => x.OrderNumber).HasMaxLength(64);
            e.Property(x => x.Amount).HasPrecision(19, 2);
            e.Property(x => x.CostAmount).HasPrecision(19, 2);
            e.HasOne<StockDocument>().WithMany().HasForeignKey(x => x.StockDocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TradeOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<FinancePayment>(e =>
        {
            e.ToTable("Payments", "finance", t => {
                t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                t.HasCheckConstraint("CK_Payments_Direction", "[Direction] IN (1,2)");
                t.HasCheckConstraint("CK_Payments_Method", "[PaymentMethodKind] = 4");
            });
            e.HasIndex(x => x.RequestId).IsUnique();
            e.HasIndex(x => new { x.PartnerId, x.PostedAtUtc });
            e.Property(x => x.Amount).HasPrecision(19, 2);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.HasOne<BusinessPartner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CatalogLookup>().WithMany().HasForeignKey(x => new { Kind = x.PaymentMethodKind, Id = x.PaymentMethodId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PaymentAllocation>(e =>
        {
            e.ToTable("Allocations", "finance", t => t.HasCheckConstraint("CK_Allocations_Amount", "[Amount] > 0"));
            e.HasKey(x => new { x.PaymentId, x.InvoiceId });
            e.Property(x => x.Amount).HasPrecision(19, 2);
        });
        // Financial postings are append-only; corrections use credits and refunds.
        foreach (var type in new[] { typeof(FinanceInvoice), typeof(FinancePayment), typeof(PaymentAllocation) })
            foreach (var property in model.Entity(type).Metadata.GetProperties().Where(x => !x.IsPrimaryKey()))
                property.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
