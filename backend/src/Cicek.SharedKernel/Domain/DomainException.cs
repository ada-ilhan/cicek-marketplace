namespace Cicek.SharedKernel.Domain;

/// <summary>
/// Thrown when code attempts to violate a domain invariant. Indicates a programming error or a race
/// that the application layer should have prevented; expected failures use <c>Result</c> instead.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message) => Code = code;

    public DomainException()
    {
        Code = "domain.error";
    }

    public DomainException(string message)
        : base(message) => Code = "domain.error";

    public DomainException(string message, Exception innerException)
        : base(message, innerException) => Code = "domain.error";

    public string Code { get; }
}
