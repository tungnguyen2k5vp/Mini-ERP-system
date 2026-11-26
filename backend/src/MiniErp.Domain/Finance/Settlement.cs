namespace MiniErp.Domain.Finance;

public sealed record SettlementInvoice(long Id,bool IsCredit,decimal Amount,decimal CashAllocated);
public sealed record InvoiceSettlement(decimal CashSettled,decimal OffsetAmount,decimal Outstanding,decimal ActionableAmount);

public static class Settlement
{
    // Credits offset the oldest unpaid debit in their source order; no cash is invented.
    public static Dictionary<long,InvoiceSettlement> Calculate(IEnumerable<SettlementInvoice> source)
    {
        var invoices=source.OrderBy(x=>x.Id).ToArray();
        if(invoices.Any(x=>x.Amount<0 || x.CashAllocated<0 || x.CashAllocated>x.Amount))
            throw ErpException.Conflict("invalid_settlement","Phân bổ vượt số tiền hóa đơn.");
        var debit=invoices.Where(x=>!x.IsCredit).Sum(x=>x.Amount-x.CashAllocated);
        var credit=invoices.Where(x=>x.IsCredit).Sum(x=>x.Amount-x.CashAllocated);
        var offset=Math.Min(debit,credit);
        var cash=invoices.Sum(x=>x.CashAllocated*(x.IsCredit?-1:1));
        var refundable=Math.Max(0,Math.Min(cash,credit-debit));
        var result=new Dictionary<long,InvoiceSettlement>();
        foreach(var isCredit in new[]{false,true})
        {
            var remainingOffset=offset;
            foreach(var invoice in invoices.Where(x=>x.IsCredit==isCredit))
            {
                var applied=Math.Min(remainingOffset,invoice.Amount-invoice.CashAllocated);
                remainingOffset-=applied;
                var outstanding=invoice.Amount-invoice.CashAllocated-applied;
                var actionable=isCredit?Math.Min(outstanding,refundable):outstanding;
                if(isCredit)refundable-=actionable;
                result.Add(invoice.Id,new(invoice.CashAllocated,applied,outstanding,actionable));
            }
        }
        return result;
    }
}
