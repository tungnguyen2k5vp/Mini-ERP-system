using MiniErp.Domain;
using MiniErp.Domain.Inventory;

namespace MiniErp.UnitTests;

public sealed class StockBalanceTests
{
    [Fact]
    public void Reservations_protect_available_stock_until_explicit_release()
    {
        var balance = new StockBalance();
        balance.Receive(10, 25);
        balance.Reserve(8);
        Assert.Equal(2, balance.Available);
        Assert.Throws<ErpException>(() => balance.Reserve(3));
        Assert.Throws<ErpException>(() => balance.Issue(3));
        Assert.Throws<ErpException>(() => balance.Reverse(3, 25));
        balance.Release(3);
        balance.Issue(3);
        Assert.Equal(7, balance.OnHand);
        Assert.Equal(5, balance.Reserved);
        Assert.Throws<ErpException>(() => balance.Release(6));
    }
    [Fact]
    public void Weighted_average_applies_to_issues_and_last_movement_reversal()
    {
        var balance = new StockBalance();
        balance.Receive(100, 100);
        balance.Receive(50, 160);
        Assert.Equal(120, balance.AverageCost);
        Assert.Equal(120, balance.Issue(20));
        Assert.Equal(130, balance.Available);
        balance.Reverse(-20, 120);
        Assert.Equal(150, balance.OnHand);
        Assert.Equal(120, balance.AverageCost);
    }

    [Fact]
    public void Rejected_issue_keeps_balance_and_cost_and_exhausted_stock_clears_cost()
    {
        var balance = new StockBalance();
        balance.Receive(10, 25);
        Assert.Equal("insufficient_stock", Assert.Throws<ErpException>(() => balance.Issue(11)).Code);
        Assert.Equal(10, balance.OnHand);
        Assert.Equal(25, balance.Issue(10));
        Assert.Equal(0, balance.OnHand);
        Assert.Equal(0, balance.AverageCost);
        balance.Receive(2, 13.5m);
        Assert.Equal(13.5m, balance.AverageCost);
    }
}
