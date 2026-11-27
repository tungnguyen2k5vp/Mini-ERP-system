using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniErp.Application;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Auth;
using MiniErp.Infrastructure.Catalog;
using MiniErp.Infrastructure.Inventory;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Orders;
using MiniErp.Infrastructure.Finance;

namespace MiniErp.Infrastructure.Persistence;

public sealed class ErpDbContext(DbContextOptions<ErpDbContext> options)
    : IdentityDbContext<ErpUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<CatalogLookup> Lookups => Set<CatalogLookup>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BusinessPartner> Partners => Set<BusinessPartner>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockDocument> StockDocuments => Set<StockDocument>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<TradeOrder> Orders => Set<TradeOrder>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderFulfillment> OrderFulfillments => Set<OrderFulfillment>();

    public Task<DocumentEvent[]> DocumentHistoryAsync(string entity, int id, CancellationToken ct)
    {
        var key = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return (from entry in AuditEntries
                join user in Users on entry.UserId equals user.Id into actors
                from user in actors.DefaultIfEmpty()
                where entry.Entity == entity && entry.EntityId == key && entry.Result == "Succeeded"
                orderby entry.Id
                select new DocumentEvent(entry.Action, entry.TimestampUtc, entry.UserId,
                    user == null ? null : user.DisplayName, entry.Reason)).ToArrayAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        CatalogModel.Configure(model);
        InventoryModel.Configure(model);
        OrderModel.Configure(model);
        FinanceModel.Configure(model);
        model.Entity<ErpUser>(user =>
        {
            user.ToTable("Users", "auth");
            user.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            user.HasIndex(x => x.NormalizedEmail).HasDatabaseName("EmailIndex").IsUnique();
        });
        model.Entity<IdentityRole<Guid>>().ToTable("Roles", "auth");
        model.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "auth");
        model.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "auth");
        model.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "auth");
        model.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "auth");
        model.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "auth");

        model.Entity<RefreshSession>(session =>
        {
            session.ToTable("RefreshSessions", "auth");
            session.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
            session.Property(x => x.SecurityStamp).HasMaxLength(100).IsRequired();
            session.Property(x => x.Version).IsRowVersion();
            session.HasIndex(x => x.TokenHash).IsUnique();
            session.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<AuditEntry>(audit =>
        {
            audit.ToTable("AuditLogs", "audit");
            audit.Property(x => x.Action).HasMaxLength(70).IsRequired();
            audit.Property(x => x.Entity).HasMaxLength(70).IsRequired();
            audit.Property(x => x.EntityId).HasMaxLength(128);
            audit.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired();
            audit.Property(x => x.Result).HasMaxLength(20).IsRequired();
            audit.Property(x => x.Before).HasMaxLength(1000);
            audit.Property(x => x.After).HasMaxLength(1000);
            audit.Property(x => x.Reason).HasMaxLength(300);
            audit.Property(x => x.InputVersion).HasMaxLength(12).IsUnicode(false);
            audit.HasIndex(x => new { x.Entity, x.EntityId, x.Id });
            audit.HasIndex(x => new { x.TimestampUtc, x.Id }).IsDescending();
        });
    }
}

public sealed class ErpDesignTimeFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Erp")
            ?? "Server=lpc:localhost;Database=MiniErp;Integrated Security=True;TrustServerCertificate=True";
        return new ErpDbContext(new DbContextOptionsBuilder<ErpDbContext>().UseSqlServer(connection).Options);
    }
}
