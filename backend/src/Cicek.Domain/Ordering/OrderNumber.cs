using System.Security.Cryptography;

namespace Cicek.Domain.Ordering;

/// <summary>
/// Human-friendly, non-sequential order numbers, e.g. <c>CS261013-7K2QX9</c>: date prefix for support staff,
/// random suffix (Crockford base32, no ambiguous characters) so numbers do not reveal order volume.
/// The 6-character suffix gives 32⁶ ≈ 10⁹ values per day; uniqueness is enforced by a database constraint and
/// the rare collision is retried by the checkout handler.
/// </summary>
public static class OrderNumber
{
    public const int Length = 15;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly System.Buffers.SearchValues<char> AlphabetValues = System.Buffers.SearchValues.Create(Alphabet);

    public static string Create(DateTimeOffset now)
    {
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"CS{now.UtcDateTime:yyMMdd}-{suffix}";
    }

    public static bool IsWellFormed(string? value) =>
        value is { Length: Length }
        && value.StartsWith("CS", StringComparison.Ordinal)
        && value[8] == '-'
        && value.AsSpan(2, 6).IndexOfAnyExceptInRange('0', '9') < 0
        && value.AsSpan(9).IndexOfAnyExcept(AlphabetValues) < 0;
}
