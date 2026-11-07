using Cicek.SharedKernel.Monetary;

namespace Cicek.Domain.Tests;

public sealed class MoneyTests
{
    private static readonly Currency Try = Currency.FromCode("TRY");
    private static readonly Currency Eur = Currency.FromCode("EUR");

    [Fact]
    public void Adds_amounts_in_the_same_currency_exactly()
    {
        var total = new Money(0.1m, Try) + new Money(0.2m, Try);

        Assert.Equal(0.3m, total.Amount);
        Assert.Equal(Try, total.Currency);
    }

    [Fact]
    public void Rejects_arithmetic_across_currencies()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(1m, Try) + new Money(1m, Eur));
    }

    [Theory]
    [InlineData("10.005", "10.01")]
    [InlineData("10.004", "10.00")]
    [InlineData("-10.005", "-10.01")]
    public void Rounds_to_minor_units_away_from_zero(string amount, string expected)
    {
        var rounded = new Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), Try).RoundToMinorUnits();

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), rounded.Amount);
    }

    [Fact]
    public void Rejects_amounts_beyond_storage_scale()
    {
        Assert.Throws<ArgumentException>(() => new Money(1.00001m, Try));
    }

    [Fact]
    public void Multiplies_by_quantity()
    {
        Assert.Equal(new Money(37.5m, Try), new Money(12.5m, Try).Multiply(3));
    }

    [Theory]
    [InlineData("try")]
    [InlineData(" TRY ")]
    public void Currency_codes_are_normalized(string code)
    {
        Assert.Equal("TRY", Currency.FromCode(code).Code);
    }

    [Fact]
    public void Unknown_currency_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Currency.FromCode("XXX"));
        Assert.False(Currency.TryFromCode("XXX", out _));
    }
}
