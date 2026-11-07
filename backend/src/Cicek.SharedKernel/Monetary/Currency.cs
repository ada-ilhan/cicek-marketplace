namespace Cicek.SharedKernel.Monetary;

/// <summary>
/// ISO-4217 currency with its minor-unit precision. Instances are created only from the known registry
/// so that arbitrary strings can never become a currency.
/// </summary>
public sealed record Currency
{
    private static readonly Dictionary<string, Currency> Known = new(StringComparer.Ordinal)
    {
        ["TRY"] = new("TRY", 2),
        ["EUR"] = new("EUR", 2),
        ["USD"] = new("USD", 2),
        ["GBP"] = new("GBP", 2),
        ["AZN"] = new("AZN", 2),
    };

    private Currency(string code, int minorUnits)
    {
        Code = code;
        MinorUnits = minorUnits;
    }

    public string Code { get; }

    /// <summary>Number of decimal places used when rounding amounts in this currency.</summary>
    public int MinorUnits { get; }

    public static Currency FromCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return Known.TryGetValue(code.Trim().ToUpperInvariant(), out var currency)
            ? currency
            : throw new ArgumentException($"Unsupported currency code '{code}'.", nameof(code));
    }

    /// <summary>ISO-4217 codes the platform can represent (used for database CHECK constraints and validation).</summary>
    public static IReadOnlyCollection<string> SupportedCodes => Known.Keys;

    public static bool TryFromCode(string? code, out Currency? currency)
    {
        currency = null;
        return !string.IsNullOrWhiteSpace(code) && Known.TryGetValue(code.Trim().ToUpperInvariant(), out currency);
    }

    public override string ToString() => Code;
}
