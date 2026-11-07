namespace Cicek.Infrastructure.Persistence.Configurations;

/// <summary>
/// Builds check-constraint SQL for enum-like columns. Oracle EF Core emits CREATE TABLE inside
/// <c>EXECUTE IMMEDIATE '…'</c> without escaping, so literals are written with doubled single quotes.
/// Values come from C# enum names (never user input).
/// </summary>
internal static class OracleCheckConstraints
{
    public static string InList(string column, IEnumerable<string> allowedValues, bool allowNull)
    {
        var values = string.Join(", ", allowedValues.Select(v =>
            v.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')
                ? $"''{v}''"
                : throw new ArgumentException($"Unsupported constraint value '{v}'.", nameof(allowedValues))));
        var inList = $"{column} IN ({values})";
        return allowNull ? $"{column} IS NULL OR {inList}" : inList;
    }
}
