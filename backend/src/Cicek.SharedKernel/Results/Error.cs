namespace Cicek.SharedKernel.Results;

/// <summary>Category of an expected failure; the API maps each kind to an HTTP status.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unavailable,
}

/// <summary>
/// An expected, non-exceptional failure. <see cref="Code"/> is a stable machine-readable identifier
/// (e.g. "auth.invalid_credentials"); <see cref="Message"/> is safe to show to end users.
/// </summary>
public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorKind.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorKind.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);

    public static Error Unavailable(string code, string message) => new(code, message, ErrorKind.Unavailable);
}
