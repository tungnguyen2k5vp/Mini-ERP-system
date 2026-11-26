using MiniErp.Domain.Finance;

namespace MiniErp.UnitTests;

public sealed class SettlementTests
{
    [Fact]
    public void Return_before_cash_offsets_debit_and_cannot_create_refundable_cash()
    {
        var open=Settlement.Calculate([new(1,false,100,0),new(2,true,40,0)]);
        Assert.Equal(60m,open[1].ActionableAmount); Assert.Equal(40m,open[1].OffsetAmount);
        Assert.Equal(0m,open[2].ActionableAmount); Assert.Equal(0m,open[2].Outstanding);
        var closed=Settlement.Calculate([new(1,false,100,60),new(2,true,40,0)]);
        Assert.All(closed.Values,x=>Assert.Equal(0m,x.Outstanding));
    }

    [Fact]
    public void Full_return_before_cash_closes_both_sources_without_payment_or_refund()
    {
        var result=Settlement.Calculate([new(1,false,100,0),new(2,true,100,0)]);
        Assert.All(result.Values,x=>Assert.Equal(0m,x.ActionableAmount));
        Assert.All(result.Values,x=>Assert.Equal(0m,x.Outstanding));
    }

    [Fact]
    public void Negative_net_invoice_cannot_refund_more_than_actual_cash_received()
    {
        var result=Settlement.Calculate([new(1,false,100,30),new(2,true,150,0)]);
        Assert.Equal(80m,result[2].Outstanding);
        Assert.Equal(30m,result[2].ActionableAmount);
        var refunded=Settlement.Calculate([new(1,false,100,30),new(2,true,150,30)]);
        Assert.Equal(50m,refunded[2].Outstanding);
        Assert.Equal(0m,refunded[2].ActionableAmount);
    }

    [Fact]
    public void Credits_offset_oldest_debits_and_refunds_are_shared_across_credit_sources()
    {
        var result=Settlement.Calculate([new(1,false,100,100),new(2,false,50,50),new(3,true,20,0),new(4,true,30,0)]);
        Assert.Equal(20m,result[3].ActionableAmount); Assert.Equal(30m,result[4].ActionableAmount);
        var beforeCash=Settlement.Calculate([new(1,false,100,0),new(2,false,50,0),new(3,true,40,0)]);
        Assert.Equal(60m,beforeCash[1].ActionableAmount); Assert.Equal(50m,beforeCash[2].ActionableAmount);
    }
}
