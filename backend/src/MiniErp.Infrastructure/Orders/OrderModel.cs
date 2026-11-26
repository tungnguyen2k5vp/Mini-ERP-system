using Microsoft.EntityFrameworkCore;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Inventory;
using MiniErp.Domain.Orders;
namespace MiniErp.Infrastructure.Orders;
internal static class OrderModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<TradeOrder>(b =>
        {
            b.ToTable("Orders", "orders", t => t.HasCheckConstraint("CK_Orders_State", "[Kind] IN (1,2) AND [Status] BETWEEN 1 AND 6 AND [WarehouseKind] = 3"));
            b.HasIndex(x => x.RequestId).IsUnique();
            b.HasIndex(x => new { x.Kind, x.PartnerId, x.Id });
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.Note).HasMaxLength(300);
            b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => new { Kind = x.WarehouseKind, Id = x.WarehouseId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OrderLine>(b =>
        {
            b.ToTable("OrderLines", "orders", t => t.HasCheckConstraint("CK_OrderLines_Amounts", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [FulfilledQuantity] BETWEEN 0 AND [Quantity] AND [ReturnedQuantity] BETWEEN 0 AND [FulfilledQuantity] AND [ReservedQuantity] >= 0 AND [ReservedQuantity] <= [Quantity] - [FulfilledQuantity]"));
            b.HasKey(x => new { x.OrderId, x.ProductId });
            b.Property(x => x.Sku).HasMaxLength(32).IsUnicode(false);
            b.Property(x => x.ProductName).HasMaxLength(160);
            foreach (var field in new[] { "Quantity", "UnitPrice", "FulfilledQuantity", "ReturnedQuantity", "ReservedQuantity" }) b.Property(field).HasPrecision(19,4);
            b.HasOne<TradeOrder>().WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OrderFulfillment>(b =>
        {
            b.ToTable("Fulfillments", "orders", t => t.HasCheckConstraint("CK_Fulfillments_Amount", "[Amount] >= 0"));
            b.HasIndex(x => x.RequestId).IsUnique();
            b.HasIndex(x => x.StockDocumentId).IsUnique();
            b.HasIndex(x => x.ReversesId).IsUnique();
            b.Property(x => x.Amount).HasPrecision(19,2);
            b.HasOne<TradeOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StockDocument>().WithMany().HasForeignKey(x => x.StockDocumentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<OrderFulfillment>().WithMany().HasForeignKey(x => x.ReversesId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
