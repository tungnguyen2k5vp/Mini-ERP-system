using System.Data;
using Microsoft.Data.SqlClient;
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
        var prior=db.Set<FinanceInvoice>().Local.SingleOrDefault(x=>x.StockDocumentId==source.StockDocumentId)
            ?? await db.Set<FinanceInvoice>().AsNoTracking().SingleOrDefaultAsync(x=>x.StockDocumentId==source.StockDocumentId,ct);
        if(prior is not null)
        {
            if(prior.OrderId!=source.OrderId || prior.OrderNumber!=source.OrderNumber || prior.PartnerId!=source.PartnerId ||
                prior.Direction!=source.Direction || prior.IsCredit!=source.IsCredit || prior.Amount!=source.Amount ||
                prior.CostAmount!=source.CostAmount || prior.IssuedAtUtc!=source.OccurredAtUtc)
                throw ErpException.Conflict("invoice_source_reused","Chứng từ nguồn đã tạo hóa đơn với nội dung khác.");
            return;
        }
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

    public Task<Page<InvoiceRow>> InvoicesAsync(FinanceDirection? direction,Guid? partnerId,int page,int size,CancellationToken ct,long? invoiceId=null)=>
        ReadWithRetryAsync(()=>ReadInvoicesAsync(direction,partnerId,page,size,ct,invoiceId),ct);

    private async Task<Page<InvoiceRow>> ReadInvoicesAsync(FinanceDirection? direction, Guid? partnerId, int page, int size, CancellationToken ct,long? invoiceId)
    {
        ValidateDirection(direction);
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var query = db.Set<FinanceInvoice>().AsNoTracking().Where(x => (!direction.HasValue || x.Direction == direction) && (!partnerId.HasValue || x.PartnerId == partnerId) && (!invoiceId.HasValue || x.Id==invoiceId));
        var total = await query.CountAsync(ct);
        var rows = await (from i in query.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            join p in db.Partners on i.PartnerId equals p.Id
            select new InvoiceRow(i.Id, i.StockDocumentId, i.OrderId, i.OrderNumber, i.PartnerId, p.Name, i.Direction,
                i.IsCredit, i.Amount, i.CostAmount, i.Allocations.Where(a => !db.Set<PaymentReversal>().Any(r => r.PaymentId == a.PaymentId)).Sum(a => a.Amount), i.Amount - i.Allocations.Where(a => !db.Set<PaymentReversal>().Any(r => r.PaymentId == a.PaymentId)).Sum(a => a.Amount), i.IssuedAtUtc, i.DueAtUtc,0m,0m)).ToArrayAsync(ct);
        var orderIds=rows.Select(x=>x.OrderId).Distinct().ToArray();
        var sources=await db.Set<FinanceInvoice>().AsNoTracking().Where(x=>orderIds.Contains(x.OrderId))
            .Select(x=>new {x.OrderId,x.Direction,Invoice=new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Allocations.Where(a => !db.Set<PaymentReversal>().Any(r => r.PaymentId == a.PaymentId)).Sum(a=>a.Amount))}).ToArrayAsync(ct);
        var settlements=sources.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>x.Invoice))).ToDictionary(x=>x.Key,x=>x.Value);
        var result=new Page<InvoiceRow>(rows.Select(row=>row with { Settled=settlements[row.Id].CashSettled,Outstanding=settlements[row.Id].Outstanding,OffsetAmount=settlements[row.Id].OffsetAmount,
            ActionableAmount=settlements[row.Id].ActionableAmount }).ToArray(), page, size, total);
        await transaction.CommitAsync(ct);
        return result;
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

    public Task<Page<PaymentRow>> PaymentsAsync(FinanceDirection? direction,Guid? partnerId,int page,int size,CancellationToken ct)=>
        ReadWithRetryAsync(()=>ReadPaymentsAsync(direction,partnerId,page,size,ct),ct);

    private async Task<Page<PaymentRow>> ReadPaymentsAsync(FinanceDirection? direction,Guid? partnerId,int page, int size, CancellationToken ct)
    {
        ValidateDirection(direction);
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var query = db.Set<FinancePayment>().AsNoTracking().Where(x=>(!direction.HasValue || x.Direction==direction) && (!partnerId.HasValue || x.PartnerId==partnerId));
        var count = await query.CountAsync(ct);
        var rows = await query.Include(x => x.Allocations).OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        var ids = rows.Select(x => x.Id).ToArray();
        var reversals = await db.Set<PaymentReversal>().Where(x => ids.Contains(x.PaymentId)).ToDictionaryAsync(x => x.PaymentId, ct);
        var result=new Page<PaymentRow>(rows.Select(x => Payment(x, reversals.GetValueOrDefault(x.Id))).ToArray(), page, size, count);
        await transaction.CommitAsync(ct);
        return result;
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
        await using var transaction = await db.BeginDocumentWriteAsync(ct);
        await db.LockFinancePartnerAsync(input.PartnerId, ct);
        await db.LockDocumentRequestAsync("finance-payment-" + input.RequestId, ct);
        foreach(var orderId in orderIds)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT [Id] FROM [orders].[Orders] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={orderId}",ct);
        var existing = await db.Set<FinancePayment>().Include(x => x.Allocations).SingleOrDefaultAsync(x => x.RequestId == input.RequestId, ct);
        if (existing is not null)
        {
            var reversal = await db.Set<PaymentReversal>().AsNoTracking().SingleOrDefaultAsync(x => x.PaymentId == existing.Id, ct);
            var row = Payment(existing, reversal);
            if (row.PartnerId != input.PartnerId || row.Direction != input.Direction || row.IsRefund != input.IsRefund || row.PaymentMethodId != input.PaymentMethodId ||
                row.Amount != input.Amount || row.Reference != input.Reference?.Trim() || !row.Allocations.OrderBy(x => x.InvoiceId).SequenceEqual(input.Allocations.OrderBy(x => x.InvoiceId)))
                throw ErpException.Conflict("request_reused", "Mã yêu cầu đã dùng với nội dung khác.");
            await transaction.CommitAsync(ct);
            return row;
        }
        if (!await db.Lookups.AnyAsync(x => x.Id == input.PaymentMethodId && x.Kind == LookupKind.PaymentMethod && x.IsActive, ct))
            throw ErpException.Invalid("payment_method", "Phương thức thanh toán không hợp lệ.");
        var invoices = await db.Set<FinanceInvoice>().AsNoTracking().Include(x => x.Allocations).Where(x => orderIds.Contains(x.OrderId)).ToArrayAsync(ct);
        var stamp = clock.GetUtcNow().UtcDateTime;
        await RequireAccountingTimeAsync(orderIds, stamp, ct);
        var allocatedPayments = invoices.SelectMany(x => x.Allocations).Select(x => x.PaymentId).Distinct().ToArray();
        var reversedIds = await db.Set<PaymentReversal>().Where(x => allocatedPayments.Contains(x.PaymentId))
            .Select(x => x.PaymentId).ToArrayAsync(ct);
        var settlements=invoices.GroupBy(x=>new{x.OrderId,x.Direction}).SelectMany(g=>Settlement.Calculate(g.Select(x=>new SettlementInvoice(x.Id,x.IsCredit,x.Amount,x.Allocations.Where(a => !reversedIds.Contains(a.PaymentId)).Sum(a=>a.Amount)))))
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
            PostedAtUtc = stamp,
            Allocations = input.Allocations.Select(x => new PaymentAllocation { InvoiceId = x.InvoiceId, Amount = x.Amount }).ToList() };
        db.Add(payment);
        db.AuditEntries.Add(AuditEntry.Create(actor, payment.PostedAtUtc, input.IsRefund ? "Finance.Refund" : "Finance.Payment", "Payment", input.RequestId.ToString()));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Payment(payment);
    }

    public async Task<PaymentRow> ReversePaymentAsync(long id, PaymentReversalInput input, RequestActor actor, CancellationToken ct)
    {
        var reason = CatalogRules.OptionalText(input.Reason);
        if (input.RequestId == Guid.Empty || reason is null || reason.Length > 300)
            throw ErpException.Invalid("invalid_payment_reversal", "Nhập mã yêu cầu và lý do đảo tối đa 300 ký tự.");
        var source = await db.Set<FinancePayment>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.PartnerId }).SingleOrDefaultAsync(ct)
            ?? throw ErpException.Missing("payment_missing", "Không tìm thấy phiếu thu chi.");
        await using var transaction = await db.BeginDocumentWriteAsync(ct);
        await db.LockFinancePartnerAsync(source.PartnerId, ct);
        await db.LockDocumentRequestAsync("finance-payment-reversal-" + input.RequestId, ct);
        var payment = await db.Set<FinancePayment>().AsNoTracking().Include(x => x.Allocations).SingleAsync(x => x.Id == id, ct);
        var previous = await db.Set<PaymentReversal>().AsNoTracking()
            .Where(x => x.PaymentId == id || x.RequestId == input.RequestId).ToArrayAsync(ct);
        if (previous.Length > 0)
        {
            var prior = previous[0];
            if (previous.Length != 1 || prior.PaymentId != id || prior.RequestId != input.RequestId || prior.Reason != reason || prior.UserId != actor.UserId)
                throw ErpException.Conflict("payment_reversal_conflict", "Phiếu hoặc mã yêu cầu đã có lần đảo khác.");
            return Payment(payment, prior);
        }
        var invoiceIds = payment.Allocations.Select(x => x.InvoiceId).ToArray();
        var sourceInvoices = await db.Set<FinanceInvoice>().AsNoTracking().Where(x => invoiceIds.Contains(x.Id)).ToArrayAsync(ct);
        if (payment.Allocations.Count == 0 || payment.Allocations.Sum(x => x.Amount) != payment.Amount ||
            sourceInvoices.Length != invoiceIds.Length || sourceInvoices.Any(x => x.PartnerId != payment.PartnerId ||
                x.Direction != payment.Direction || x.IsCredit != payment.IsRefund || x.IssuedAtUtc > payment.PostedAtUtc))
            throw ErpException.Conflict("invalid_payment_scope", "Cần đối soát phiếu gốc và phân bổ trước khi đảo.");
        var orders = await db.Set<FinanceInvoice>().AsNoTracking().Where(x => invoiceIds.Contains(x.Id))
            .Select(x => x.OrderId).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        foreach (var orderId in orders)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM orders.Orders WITH (UPDLOCK,HOLDLOCK) WHERE Id={orderId}", ct);
        var stamp = clock.GetUtcNow().UtcDateTime;
        if (stamp < payment.PostedAtUtc)
            throw ErpException.Conflict("reversal_before_payment", "Thời điểm đảo không được trước phiếu gốc.");
        await RequireAccountingTimeAsync(orders, stamp, ct);
        var cash = await (from allocation in db.Set<PaymentAllocation>()
                          join invoice in db.Set<FinanceInvoice>() on allocation.InvoiceId equals invoice.Id
                          join paid in db.Set<FinancePayment>() on allocation.PaymentId equals paid.Id
                          where orders.Contains(invoice.OrderId) && paid.Id != id &&
                              !db.Set<PaymentReversal>().Any(x => x.PaymentId == paid.Id)
                          select new { invoice.OrderId, invoice.Direction, invoice.IsCredit,
                              InvoicePartner = invoice.PartnerId, paid.PartnerId, PaymentDirection = paid.Direction,
                              paid.IsRefund, allocation.Amount,
                              ValidPayment = paid.Amount == paid.Allocations.Sum(x => x.Amount) &&
                                  !paid.Allocations.Any(x => !db.Set<FinanceInvoice>().Any(i => i.Id == x.InvoiceId &&
                                      i.PartnerId == paid.PartnerId && i.Direction == paid.Direction &&
                                      i.IsCredit == paid.IsRefund && i.IssuedAtUtc <= paid.PostedAtUtc)) }).ToArrayAsync(ct);
        if (cash.Any(x => !x.ValidPayment || x.InvoicePartner != x.PartnerId || x.Direction != x.PaymentDirection || x.IsCredit != x.IsRefund))
            throw ErpException.Conflict("invalid_payment_scope", "Cần đối soát phân bổ thu chi trước khi đảo.");
        if (cash.GroupBy(x => new { x.OrderId, x.Direction })
            .Any(g => g.Sum(x => x.IsRefund ? -x.Amount : x.Amount) < 0))
            throw ErpException.Conflict("dependent_refund", "Hãy đảo tiền hoàn phụ thuộc trước khi đảo phiếu thu chi.");
        var reversal = new PaymentReversal
        {
            PaymentId = id, RequestId = input.RequestId, PostedAtUtc = stamp, Reason = reason, UserId = actor.UserId
        };
        db.Add(reversal);
        db.AuditEntries.Add(AuditEntry.Create(actor, stamp, "Finance.ReversePayment", "Payment", id.ToString(), reason: reason));
        await db.SaveChangesAsync(ct);
        var result = Payment(payment, reversal);
        await transaction.CommitAsync(ct);
        return result;
    }

    private static PaymentRow Payment(FinancePayment x, PaymentReversal? reversal = null) => new(x.Id, x.RequestId, x.PartnerId, x.Direction, x.IsRefund,
        x.PaymentMethodId, x.Amount, x.Reference, x.PostedAtUtc, x.Allocations.Select(a => new AllocationInput(a.InvoiceId, a.Amount)).ToArray(),
        reversal is null ? null : new(reversal.Id, reversal.RequestId, reversal.PostedAtUtc, reversal.Reason));

    private async Task RequireAccountingTimeAsync(int[] orderIds, DateTime stamp, CancellationToken ct)
    {
        var invoices = db.Set<FinanceInvoice>().Where(x => orderIds.Contains(x.OrderId));
        var payments = db.Set<FinancePayment>().Where(x => x.Allocations.Any(a => invoices.Any(i => i.Id == a.InvoiceId)));
        if (await invoices.AnyAsync(x => x.IssuedAtUtc > stamp, ct) ||
            await payments.AnyAsync(x => x.PostedAtUtc > stamp, ct) ||
            await db.Set<PaymentReversal>().AnyAsync(x => x.PostedAtUtc > stamp && payments.Any(p => p.Id == x.PaymentId), ct))
            throw ErpException.Conflict("backdated_settlement", "Thời điểm thu chi hoặc đảo không được trước sự kiện công nợ liên quan.");
    }
    private static async Task<T> ReadWithRetryAsync<T>(Func<Task<T>> read,CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            try{return await read();}
            catch(Exception error)when(attempt<2 && IsDeadlock(error))
            {
                // Only a whole pure read is replayed, after its failed transaction has been disposed.
                await Task.Delay(TimeSpan.FromMilliseconds(25*(attempt+1)),ct);
            }
        }
    }
    private static bool IsDeadlock(Exception? error)
    {
        while(error is not null){if(error is SqlException{Number:1205})return true;error=error.InnerException;}
        return false;
    }
    public static void ValidateDirection(FinanceDirection? direction)
    {
        if (direction.HasValue && !Enum.IsDefined(direction.Value)) throw ErpException.Invalid("finance_direction", "Loại công nợ không hợp lệ.");
    }
}
