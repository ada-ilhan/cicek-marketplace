namespace Cicek.SharedKernel.Monetary;

/// <summary>
/// Monetary amount in a specific currency. Uses <see cref="decimal"/> only; arithmetic across
/// different currencies is rejected. Persisted as NUMBER(19,4) plus a CHAR(3) currency code.
/// </summary>
public readonly record struct Money
{
    /// <summary>Scale used for storage (NUMBER(19,4)); rounding to currency minor units is explicit.</summary>
    public const int StorageScale = 4;

    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (decimal.Round(amount, StorageScale) != amount)
        {
            throw new ArgumentException($"Amount exceeds storage scale of {StorageScale} decimal places.", nameof(amount));
        }

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public static Money Zero(Currency currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(int quantity) => new(Amount * quantity, Currency);

    /// <summary>Rounds to the currency's minor units, away from zero on midpoints (commercial rounding).</summary>
    public Money RoundToMinorUnits() =>
        new(decimal.Round(Amount, Currency.MinorUnits, MidpointRounding.AwayFromZero), Currency);

    public bool IsNegative => Amount < 0m;

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public override string ToString() => $"{Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Currency.Code}";

    private void EnsureSameCurrency(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot combine {Currency.Code} with {other.Currency.Code}.");
        }
    }
}
