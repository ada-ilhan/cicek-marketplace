namespace Cicek.Application.Abstractions.Persistence;

/// <summary>Provider-neutral classification of a database failure.</summary>
public enum DatabaseFailureKind
{
    /// <summary>Unique or primary key constraint violated.</summary>
    UniqueViolation,

    /// <summary>Referenced parent row missing, or child rows still reference the row.</summary>
    ReferenceViolation,

    /// <summary>Optimistic concurrency token mismatch.</summary>
    ConcurrencyConflict,

    /// <summary>Deadlock or serialization failure; the whole unit of work may be retried if it is idempotent.</summary>
    TransientContention,

    /// <summary>Database unreachable, connection lost, timeout or authentication failure at connect time.</summary>
    Unavailable,

    /// <summary>A PL/SQL package raised a documented application error (ORA-20000..ORA-20999).</summary>
    ApplicationRule,

    /// <summary>Any other database error; treated as an internal server error.</summary>
    Unknown,
}

/// <param name="Kind">Classification.</param>
/// <param name="ProviderCode">Provider error number (e.g. Oracle ORA code) for logs only; never sent to clients.</param>
/// <param name="ApplicationCode">For <see cref="DatabaseFailureKind.ApplicationRule"/>: the stable code from the PL/SQL error contract.</param>
public sealed record DatabaseFailure(DatabaseFailureKind Kind, int? ProviderCode, string? ApplicationCode = null);

/// <summary>
/// Translates provider exceptions into <see cref="DatabaseFailure"/>. Keeps Oracle types inside
/// Infrastructure while letting the API return safe Problem Details.
/// </summary>
public interface IDatabaseErrorClassifier
{
    /// <summary>Returns null when the exception is not a database failure.</summary>
    DatabaseFailure? Classify(Exception exception);
}
