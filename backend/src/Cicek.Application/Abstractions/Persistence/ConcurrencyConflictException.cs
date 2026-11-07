namespace Cicek.Application.Abstractions.Persistence;

/// <summary>
/// Raised by persistence adapters when an optimistic concurrency check fails, so that use cases can
/// react without depending on EF Core or Oracle exception types.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
