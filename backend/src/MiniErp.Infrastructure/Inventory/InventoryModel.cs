using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Inventory;

namespace MiniErp.Infrastructure.Inventory;

internal static class InventoryModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<StockBalance>(row =>
        {
            row.ToTable("Balances", "inventory", t =>
            {
                t.HasCheckConstraint("CK_Balances_Amounts", "[OnHand] >= [Reserved] AND [Reserved] >= 0 AND [AverageCost] >= 0");
                t.HasCheckConstraint("CK_Balances_Warehouse", "[WarehouseKind] = 3");
            });
            row.HasKey(x => new { x.WarehouseId, x.ProductId });
            row.Property(x => x.Version).IsRowVersion();
            Precision(row, nameof(StockBalance.OnHand), nameof(StockBalance.Reserved), nameof(StockBalance.AverageCost));
            row.HasOne<CatalogLookup>().WithMany().HasForeignKey(x => new { Kind = x.WarehouseKind, Id = x.WarehouseId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockDocument>(doc =>
        {
            doc.ToTable("Documents", "inventory", t =>
            {
                t.HasCheckConstraint("CK_Documents_Kind", "[Kind] BETWEEN 1 AND 6 AND [WarehouseKind] = 3");
                t.HasCheckConstraint("CK_Documents_Status", "([Status] IN (1,3) AND [PostedAtUtc] IS NULL) OR ([Status] IN (2,4) AND [PostedAtUtc] IS NOT NULL)");
                t.HasCheckConstraint("CK_Documents_Destination", "([Kind] = 3 AND [DestinationId] IS NOT NULL AND [DestinationId] <> [WarehouseId]) OR [Kind] = 6 OR ([Kind] NOT IN (3,6) AND [DestinationId] IS NULL)");
                t.HasCheckConstraint("CK_Documents_Reversal", "([Kind] = 6 AND [ReversesId] IS NOT NULL) OR ([Kind] <> 6 AND [ReversesId] IS NULL)");
            });
            doc.HasIndex(x => x.RequestId).IsUnique();
            doc.HasIndex(x => x.ReversesId).IsUnique();
            doc.HasIndex(x => new { x.Kind, x.Id }).IsDescending(false, true);
            doc.Property(x => x.Note).HasMaxLength(300);
            doc.Property(x => x.CreatePayloadHash).HasMaxLength(64).IsUnicode(false);
            doc.Property(x => x.IsEdited).HasDefaultValue(false);
            doc.Property(x => x.Version).IsRowVersion();
            doc.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => new { Kind = x.WarehouseKind, Id = x.WarehouseId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            doc.HasOne(x => x.Destination).WithMany().HasForeignKey(x => new { Kind = x.WarehouseKind, Id = x.DestinationId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            doc.HasOne<StockDocument>().WithMany().HasForeignKey(x => x.ReversesId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockLine>(line =>
        {
            line.ToTable("DocumentLines", "inventory", t =>
            {
                t.HasCheckConstraint("CK_StockLines_Cost", "[UnitCost] IS NULL OR [UnitCost] >= 0");
                t.HasCheckConstraint("CK_StockLines_PostedSalesCost", "[PostedSalesCost] IS NULL OR [PostedSalesCost] >= 0");
            });
            line.HasKey(x => new { x.DocumentId, x.ProductId });
            line.Property(x => x.Sku).HasMaxLength(32).IsUnicode(false);
            line.Property(x => x.ProductName).HasMaxLength(160);
            line.Property(x => x.CountVersion).HasMaxLength(8);
            Precision(line, nameof(StockLine.Quantity), nameof(StockLine.UnitCost));
            line.Property(x => x.PostedSalesCost).HasPrecision(19, 2);
            line.HasOne<StockDocument>().WithMany(x => x.Lines).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            line.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StockMovement>(movement =>
        {
            movement.ToTable("Movements", "inventory", t =>
            {
                t.HasCheckConstraint("CK_Movements_Amounts", "[QuantityDelta] <> 0 AND [BalanceAfter] >= 0 AND [UnitCost] >= 0 AND [PreviousAverageCost] >= 0");
                t.HasCheckConstraint("CK_Movements_Warehouse", "[WarehouseKind] = 3");
            });
            movement.HasIndex(x => new { x.DocumentId, x.WarehouseId, x.ProductId }).IsUnique();
            movement.HasIndex(x => new { x.WarehouseId, x.ProductId, x.Id }).IsDescending(false, false, true);
            Precision(movement, nameof(StockMovement.QuantityDelta), nameof(StockMovement.UnitCost),
                nameof(StockMovement.PreviousAverageCost), nameof(StockMovement.BalanceAfter));
            movement.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<CatalogLookup>().WithMany().HasForeignKey(x => new { Kind = x.WarehouseKind, Id = x.WarehouseId })
                .HasPrincipalKey(x => new { x.Kind, x.Id }).OnDelete(DeleteBehavior.Restrict);
            movement.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void Precision<T>(EntityTypeBuilder<T> builder, params string[] fields) where T : class
    {
        foreach (var field in fields) builder.Property(field).HasPrecision(19, 4);
    }
}
