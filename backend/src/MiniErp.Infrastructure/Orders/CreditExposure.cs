using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Orders;
using MiniErp.Domain.Finance;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Orders;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Orders;

public static class CustomerCredit
{
    public static async Task<CreditExposure> ForPartnerAsync(ErpDbContext db, Guid partnerId, CancellationToken ct)
    {
        await using var read = await db.BeginDocumentReadAsync(ct);
        // Preserve the same customer view when a caller supplies READ COMMITTED/RCSI.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM catalog.BusinessPartners WITH (HOLDLOCK) WHERE Id={partnerId}", ct);
        var result = await CalculateAsync(db, partnerId, 0, false, ct);
        if (read is not null) await read.CommitAsync(ct);
        return result;
    }
    public static async Task<CreditExposure> ForOrderAsync(ErpDbContext db, int id, CancellationToken ct)
    {
        // Discover the mutable draft partner without retaining an order lock before its customer.
        var partner = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.Orders WITH (READCOMMITTED) WHERE Id={id}")
            .AsNoTracking().Select(x => new { x.PartnerId, x.Kind }).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("order_missing", "Không tìm thấy đơn.");
        if (partner.Kind != OrderKind.Sales) throw ErpException.Invalid("sales_order_required", "Chỉ xem hạn mức cho đơn bán hàng.");
        await using var read = await db.BeginDocumentReadAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM catalog.BusinessPartners WITH (HOLDLOCK) WHERE Id={partner.PartnerId}", ct);
        var order = await db.Orders.FromSqlInterpolated($"SELECT * FROM orders.Orders WITH (HOLDLOCK) WHERE Id={id}")
            .AsNoTracking().Include(x => x.Lines).SingleAsync(ct);
        if (order.PartnerId != partner.PartnerId)
            throw ErpException.Conflict("stale_version", "Đối tác của đơn đã thay đổi. Hãy tải lại.");
        var result = await ForLockedOrderAsync(db, order, ct);
        if (read is not null) await read.CommitAsync(ct);
        return result;
    }
    internal static Task<CreditExposure> ForLockedOrderAsync(ErpDbContext db, TradeOrder order, CancellationToken ct) =>
        CalculateAsync(db, order.PartnerId, order.Lines.Sum(x => Money.Round(x.Quantity * x.UnitPrice)),
            order.Status is OrderStatus.Draft or OrderStatus.PendingApproval or OrderStatus.ReturnedForRevision, ct);
    private static async Task<CreditExposure> CalculateAsync(ErpDbContext db, Guid partnerId, decimal amount, bool addOrder, CancellationToken ct)
    {
        var limit = await db.Partners.Where(x => x.Id == partnerId && x.Kind == PartnerKind.Customer)
            .Select(x => (decimal?)x.CreditLimit).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("customer_missing", "Không tìm thấy khách hàng.");
        var outstanding = await db.Database.SqlQuery<decimal>($"""
            SELECT COALESCE(SUM(CASE WHEN debt.Net > 0 THEN debt.Net ELSE 0 END),0) AS [Value]
            FROM (
                SELECT i.OrderId, CAST(SUM(CASE WHEN i.IsCredit=1 THEN -(i.Amount-COALESCE(a.Amount,0))
                    ELSE i.Amount-COALESCE(a.Amount,0) END) AS decimal(28,2)) AS Net
                FROM finance.Invoices i
                OUTER APPLY (
                    SELECT CAST(SUM(a.Amount) AS decimal(28,2)) AS Amount FROM finance.Allocations a
                    WHERE a.InvoiceId=i.Id AND NOT EXISTS (
                        SELECT 1 FROM finance.PaymentReversals r WHERE r.PaymentId=a.PaymentId)
                ) a
                WHERE i.PartnerId={partnerId} AND i.Direction=1
                GROUP BY i.OrderId
            ) debt
            """).SingleAsync(ct);
        var unbilled = await db.Database.SqlQuery<decimal>($"""
            SELECT COALESCE(SUM(CAST(ROUND(CAST(l.Quantity AS decimal(16,4))*CAST(l.UnitPrice AS decimal(16,4)),2)
                -ROUND(CAST(l.FulfilledQuantity AS decimal(16,4))*CAST(l.UnitPrice AS decimal(16,4)),2) AS decimal(28,2))),0) AS [Value]
            FROM orders.Orders o JOIN orders.OrderLines l ON l.OrderId=o.Id
            WHERE o.PartnerId={partnerId} AND o.Kind=2 AND o.Status IN (3,4)
            """).SingleAsync(ct);
        var used = outstanding + unbilled;
        var projected = used + (addOrder ? amount : 0);
        return new(limit, outstanding, unbilled, used, limit == 0 ? null : Math.Max(0, limit - used), amount, projected, limit > 0 && projected > limit);
    }
}
