using Gba.TradeLicense.Api.Services;
using Xunit;

namespace Gba.TradeLicense.Api.Tests;

public class LicenceFeeCalculatorTests
{
    [Theory]
    // trade fees, power HP, generator HP, expected = trades + max(HP) x 100 + 500
    [InlineData(new double[] { 1200, 800 }, 3, 5, 3000)]
    [InlineData(new double[] { 1200 }, 0, 0, 1700)]
    [InlineData(new double[] { 1000 }, 10, 2, 2500)]      // larger HP is power
    [InlineData(new double[] { 1000 }, 2, 10, 2500)]      // larger HP is generator
    [InlineData(new double[] { 0 }, 0, 0, 500)]           // fee not configured -> 0, as the form
    [InlineData(new double[] { 1000 }, 2.5, 0, 1750)]     // fractional HP
    public void Payable_FollowsTheExistingFormula(double[] trades, double powerHp, double generatorHp, double expected)
    {
        var fee = LicenceFeeCalculator.Payable(trades.Select(t => (decimal)t), (decimal)powerHp, (decimal)generatorHp);
        Assert.Equal((decimal)expected, fee);
    }

    [Fact]
    public void Payable_MatchesTheBrowserForAwkwardHpValues()
    {
        // The form computes in JavaScript floating point (2.555 * 100 = 255.49999999999997);
        // the amount sent is rounded to 2 decimals by the API, which must equal the server fee.
        var browserAmount = decimal.Round((decimal)(1000 + 2.555 * 100 + 500), 2);
        Assert.Equal(browserAmount, LicenceFeeCalculator.Payable(new[] { 1000m }, 2.555m, 0m));
    }

    [Fact]
    public void MinimumWithoutHp_IsTradesPlus500()
    {
        Assert.Equal(2500m, LicenceFeeCalculator.MinimumWithoutHp(new[] { 1200m, 800m }));
    }

    [Theory]
    [InlineData(null, true)]       // not sent (older client)
    [InlineData(0.0, true)]
    [InlineData(12.5, true)]
    [InlineData(2.5555, true)]     // 4 decimals = storage precision
    [InlineData(-1.0, false)]      // negative
    [InlineData(2.55555, false)]   // more precision than stored
    [InlineData(1e9, false)]       // beyond the column size
    public void ValidateHp(double? value, bool valid)
    {
        var error = LicenceFeeCalculator.ValidateHp(value is null ? null : (decimal)value.Value, "HP");
        Assert.Equal(valid, error == null);
    }
}
