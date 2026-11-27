using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MiniErp.Infrastructure.Persistence;

internal sealed class DocumentTransaction(ErpDbContext db, IDbContextTransaction transaction) : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

    public async ValueTask DisposeAsync()
    {
        // EF accepts a successful SaveChanges even if a later step rolls its SQL transaction back.
        try { await transaction.DisposeAsync(); }
        finally { db.ChangeTracker.Clear(); }
    }
}

internal static class DocumentTransactions
{
    public static async Task<DocumentTransaction> BeginDocumentWriteAsync(this ErpDbContext db, CancellationToken ct)
    {
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Save pending changes before starting a document command.");
        db.ChangeTracker.Clear();
        return new DocumentTransaction(db, await db.Database.BeginTransactionAsync(ct));
    }

    public static async Task<IDbContextTransaction?> BeginDocumentReadAsync(this ErpDbContext db, CancellationToken ct) =>
        db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;

    // Credit approval, cash posting and corrections take this lock before touching the partner's orders.
    public static Task LockFinancePartnerAsync(this ErpDbContext db, Guid partnerId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM catalog.BusinessPartners WITH (UPDLOCK,HOLDLOCK) WHERE Id={partnerId}", ct);

    public static Task LockDocumentRequestAsync(this ErpDbContext db, string resource, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, 'Document request lock could not be acquired.', 1;
            """, ct);
}
