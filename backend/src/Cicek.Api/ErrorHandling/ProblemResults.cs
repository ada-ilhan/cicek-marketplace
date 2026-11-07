using Cicek.SharedKernel.Results;

namespace Cicek.Api.ErrorHandling;

/// <summary>Maps expected application failures (<see cref="Error"/>) to RFC 9457 Problem Details.</summary>
internal static class ProblemResults
{
    public const string CodeExtension = "code";

    public static IResult ToProblem(this Error error) =>
        TypedResults.Problem(
            statusCode: StatusFor(error.Kind),
            title: TitleFor(error.Kind),
            detail: error.Message,
            extensions: new Dictionary<string, object?> { [CodeExtension] = error.Code });

    public static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => "Invalid request",
        ErrorKind.NotFound => "Not found",
        ErrorKind.Conflict => "Conflict",
        ErrorKind.Unauthorized => "Unauthorized",
        ErrorKind.Forbidden => "Forbidden",
        ErrorKind.Unavailable => "Service unavailable",
        _ => "Error",
    };
}
