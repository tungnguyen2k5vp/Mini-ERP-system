using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application;
using MiniErp.Application.Finance;
using MiniErp.Domain;
using MiniErp.Domain.Catalog;
using MiniErp.Domain.Finance;
using MiniErp.Domain.Inventory;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Finance;

public sealed class FinanceService(ErpDbContext db, TimeProvider clock) : IFinancePosting
{
    public async Task AddInvoiceAsync(FinanceSource source, CancellationToken ct)
    {
        if (source.StockDocumentId <= 0 || source.OrderId <= 0 || string.IsNullOrWhiteSpace(source.OrderNumber) || source.OrderNumber.Length > 64 ||
            !Enum.IsDefined(source.Direction) || source.OccurredAtUtc.Kind != DateTimeKind.Utc)
            throw ErpException.Invalid("invalid_finance_source", "Nguồn hóa đơn không hợp lệ.");
        Money.Validate(source.Amount, true);
        Money.Validate(source.CostAmount, true);
        if (db.Set<FinanceInvoice>().Local.Any(x => x.StockDocumentId == source.StockDocumentId) ||
            await db.Set<FinanceInvoice>().AnyAsync(x => x.StockDocumentId == source.StockDocumentId, ct)) return;
        if (!await db.StockDocuments.AnyAsync(x => x.Id == source.StockDocumentId && x.Status == StockStatus.Posted, ct))
            throw ErpException.Invalid("unposted_invoice_source", "Hóa đơn cần chứng từ kho đã ghi sổ.");
        var partner = await db.Partners.SingleOrDefaultAsync(x => x.Id == source.PartnerId, ct)
            ?? throw ErpException.Missing("partner_missing", "Không tìm thấy đối tác.");
        if ((source.Direction == FinanceDirection.Receivable) != (partner.Kind == PartnerKind.Customer))
            throw ErpException.Invalid("partner_kind", "Loại đối tác không khớp hóa đơn.");
        db.Add(new FinanceInvoice { StockDocumentId = source.StockDocumentId, OrderId = source.OrderId,
            OrderNumber = source.OrderNumber, PartnerId = source.PartnerId, Direction = source.Direction,
            IsCredit = source.IsCredit, Amount = source.Amount, CostAmount = source.CostAmount,
            IssuedAtUtc = source.OccurredAtUtc, DueAtUtc = source.OccurredAtUtc.AddDays(partner.PaymentTermsDays) });
    }

    public async Task<Page<InvoiceRow>> InvoicesAsync(FinanceDirection? direction, Guid? partnerId, int page, int size, CancellationToken ct,long? invoiceId=null)
    {
        ValidateDirection(direction);
        var query = db.Set<FinanceInvoice>().AsNoTracking().Where(x => (!direction.HasValue || x.Direction == direction) && (!partnerId.HasValue || x.PartnerId == partnerId) && (!invoiceId.HasValue || x.Id==invoiceId));
        var total = await query.CountAsync(ct);
        var rows = await (from i in query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            join p in db.Partners on i.PartnerId equals p.Id
            select new InvoiceRow(i.Id, i.StockDocumentId, i.OrderId, i.OrderNumber, i.PartnerId, p.Name, i.Direction,
                i.IsCredit, i.Amount, i.CostAmount, i.Allocations.Sum(a => a.Amount), i.Amount - i.Allocations.Sum(a => a.Amount), i.IssuedAtUtc, i.DueAtUtc,0m,0m)).ToArrayAsync(ct);
        var orderIds=rows.Select(x=>x.OrderId).Distinct().ToArray();
        var sources=await db.Set<FinanceInvoice>().AsNoTracking().Where(x=>orderIds.Contains(x.OrderId))
            .Select(x=>new {x.OrderId,x.Direction,Invoice=new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Allocations.Sum(a=>a.Amount))}).ToArrayAsync(ct);
        var settlements=sources.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>x.Invoice))).ToDictionary(x=>x.Key,x=>x.Value);
        return new(rows.Select(row=>row with { Settled=settlements[row.Id].CashSettled,Outstanding=settlements[row.Id].Outstanding,OffsetAmount=settlements[row.Id].OffsetAmount,
            ActionableAmount=settlements[row.Id].ActionableAmount }).ToArray(), page, size, total);
    }

    public async Task<FinanceOptions> OptionsAsync(FinanceDirection direction,CancellationToken ct)
    {
        ValidateDirection(direction);
        var kind=direction==FinanceDirection.Receivable?PartnerKind.Customer:PartnerKind.Supplier;
        var partners=await db.Partners.AsNoTracking().Where(x=>x.Kind==kind).OrderBy(x=>x.Name)
            .Select(x=>new FinanceOption(x.Id,x.Name,x.IsActive)).ToArrayAsync(ct);
        var methods=await db.Lookups.AsNoTracking().Where(x=>x.Kind==LookupKind.PaymentMethod && x.IsActive).OrderBy(x=>x.Name)
            .Select(x=>new FinanceOption(x.Id,x.Name,x.IsActive)).ToArrayAsync(ct);
        return new(partners,methods);
    }

    public async Task<Page<PaymentRow>> PaymentsAsync(FinanceDirection? direction,Guid? partnerId,int page, int size, CancellationToken ct)
    {
        ValidateDirection(direction);
        var query = db.Set<FinancePayment>().AsNoTracking().Where(x=>(!direction.HasValue || x.Direction==direction) && (!partnerId.HasValue || x.PartnerId==partnerId));
        var count = await query.CountAsync(ct);
        var rows = await query.Include(x => x.Allocations).OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(rows.Select(Payment).ToArray(), page, size, count);
    }

    public async Task<PaymentRow> PayAsync(PaymentInput input, RequestActor actor, CancellationToken ct)
    {
        ValidateDirection(input.Direction);
        Money.Validate(input.Amount);
        if (input.RequestId == Guid.Empty || input.PartnerId == Guid.Empty || input.Allocations is not { Length: > 0 and <= 100 } ||
            input.Allocations.Any(x => x is null) || input.Reference?.Length > 100)
            throw ErpException.Invalid("invalid_payment", "Cần mã yêu cầu và từ 1 đến 100 phân bổ.");
        foreach (var a in input.Allocations) Money.Validate(a.Amount);
        if (input.Allocations.Select(x => x.InvoiceId).Distinct().Count() != input.Allocations.Length || input.Allocations.Sum(x => x.Amount) != input.Amount)
            throw ErpException.Invalid("invalid_allocation", "Phân bổ không trùng hóa đơn và tổng phải bằng số tiền thanh toán.");
        var ids = input.Allocations.Select(x => x.InvoiceId).ToArray();
        // Immutable invoice source IDs may be discovered before locking; their order is the serialization boundary.
        var orderIds=await db.Set<FinanceInvoice>().AsNoTracking().Where(x=>ids.Contains(x.Id)).Select(x=>x.OrderId).Distinct().OrderBy(x=>x).ToArrayAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        foreach(var orderId in orderIds)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [orders].[Orders] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={orderId}",ct);
        var existing = await db.Set<FinancePayment>().Include(x => x.Allocations).SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (existing is not null)
        {
            var row = Payment(existing);
            if (row.PartnerId != input.PartnerId || row.Direction != input.Direction || row.IsRefund != input.IsRefund || row.PaymentMethodId != input.PaymentMethodId ||
                row.Amount != input.Amount || row.Reference != input.Reference?.Trim() || !row.Allocations.OrderBy(x => x.InvoiceId).SequenceEqual(input.Allocations.OrderBy(x => x.InvoiceId)))
                throw ErpException.Conflict("request_reused", "Mã yêu cầu đã dùng với nội dung khác.");
            await transaction.CommitAsync(ct);
            return row;
        }
        if (!await db.Lookups.AnyAsync(x => x.Id == input.PaymentMethodId && x.Kind == LookupKind.PaymentMethod && x.IsActive, ct))
            throw ErpException.Invalid("payment_method", "Phương thức thanh toán không hợp lệ.");
        var invoices = await db.Set<FinanceInvoice>().AsNoTracking().Include(x => x.Allocations).Where(x => orderIds.Contains(x.OrderId)).ToArrayAsync(ct);
        var settlements=invoices.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Allocations.Sum(a=>a.Amount)))))
            .ToDictionary(x=>x.Key,x=>x.Value);
        foreach (var allocation in input.Allocations)
        {
            var invoice = invoices.SingleOrDefault(x => x.Id == allocation.InvoiceId)
                ?? throw ErpException.Missing("invoice_missing", "Không tìm thấy hóa đơn.");
            if (invoice.PartnerId != input.PartnerId || invoice.Direction != input.Direction || invoice.IsCredit != input.IsRefund)
                throw ErpException.Invalid("allocation_mismatch", "Phân bổ phải cùng đối tác, loại công nợ và loại hoàn tiền.");
            if (allocation.Amount > settlements[invoice.Id].ActionableAmount)
                throw ErpException.Conflict("overpayment", "Số tiền phân bổ vượt công nợ còn lại.");
        }
        var payment = new FinancePayment { RequestId = input.RequestId, PartnerId = input.PartnerId, Direction = input.Direction,
            IsRefund = input.IsRefund, PaymentMethodId = input.PaymentMethodId, Amount = input.Amount, Reference = input.Reference?.Trim(),
            PostedAtUtc = clock.GetUtcNow().UtcDateTime,
            Allocations = input.Allocations.Select(x => new PaymentAllocation { InvoiceId = x.InvoiceId, Amount = x.Amount }).ToList() };
        db.Add(payment);
        db.AuditEntries.Add(AuditEntry.Create(actor, payment.PostedAtUtc, input.IsRefund ? "Finance.Refund" : "Finance.Payment", "Payment", input.RequestId.ToString()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Payment(payment);
    }

    private static PaymentRow Payment(FinancePayment x) => new(x.Id, x.RequestId, x.PartnerId, x.Direction, x.IsRefund,
        x.PaymentMethodId, x.Amount, x.Reference, x.PostedAtUtc, x.Allocations.Select(a => new AllocationInput(a.InvoiceId, a.Amount)).ToArray());
    public static void ValidateDirection(FinanceDirection? direction)
    {
        if (direction.HasValue && !Enum.IsDefined(direction.Value)) throw ErpException.Invalid("finance_direction", "Loại công nợ không hợp lệ.");
    }
}
