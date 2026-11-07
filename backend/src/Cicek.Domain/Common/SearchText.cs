using System.Globalization;
using System.Text;

namespace Cicek.Domain.Common;

/// <summary>
/// Culture-independent text normalisation for search columns and URL slugs. Turkish letters are folded
/// explicitly (ı/İ are not handled correctly by invariant upper/lower-casing or by database UPPER()).
/// </summary>
public static class SearchText
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            builder.Append(c switch
            {
                'ı' or 'I' or 'İ' or 'i' => 'i',
                'ç' or 'Ç' => 'c',
                'ğ' or 'Ğ' => 'g',
                'ö' or 'Ö' => 'o',
                'ş' or 'Ş' => 's',
                'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(c),
            });
        }

        // Remove remaining diacritics (é → e) and collapse whitespace.
        var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    result.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            result.Append(c);
            lastWasSpace = false;
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>URL slug: normalised, letters/digits only, words joined by '-', at most <paramref name="maxLength"/> characters.</summary>
    public static string Slugify(string? text, int maxLength = 80)
    {
        var normalized = Normalize(text);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return slug;
    }
}
