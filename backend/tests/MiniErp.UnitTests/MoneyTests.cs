using MiniErp.Domain;
using MiniErp.Domain.Finance;

namespace MiniErp.UnitTests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("1.005", "1.01")]
    [InlineData("2.0049", "2.00")]
    [InlineData("-1.005", "-1.01")]
    public void Currency_rounds_half_away_from_zero(string source, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Money.Round(decimal.Parse(source, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Payment_amounts_reject_negative_zero_excess_precision_and_overflow()
    {
        foreach (var value in new[] { -1m, 0m, 1.001m, 1_000_000_000_000m })
            Assert.Throws<ErpException>(() => Money.Validate(value));
        Assert.Equal(0m, Money.Validate(0m, true));
        Assert.Equal(1.01m, Money.Validate(1.01m));
    }
}
