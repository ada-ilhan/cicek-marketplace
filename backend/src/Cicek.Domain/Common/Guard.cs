using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Common;

/// <summary>Trimming text guards shared by aggregates. Input is pre-validated by the application layer;
/// these checks protect invariants against programming errors.</summary>
public static class Guard
{
    public static string RequireText(string? value, int maxLength, string code)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException(code, $"Value is required and must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    /// <summary>
    /// Optional text is stored as null when blank: Oracle treats an empty string as NULL, so "" can never be
    /// round-tripped and would violate NOT NULL constraints.
    /// </summary>
    public static string? OptionalText(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength
            ? trimmed
            : throw new DomainException("text.too_long", $"Value must be at most {maxLength} characters.");
    }
}
