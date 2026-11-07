using Cicek.Application.Abstractions.Persistence;
using Cicek.Domain.Ordering;
using Cicek.SharedKernel.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.ErrorHandling;

/// <summary>
/// Last line of defence for unhandled exceptions. Responses never contain exception messages, ORA codes,
/// SQL, schema names or stack traces; those go to the server log only (correlated by traceId).
/// </summary>
internal sealed class GlobalExceptionHandler(
    IDatabaseErrorClassifier databaseErrors,
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing useful can be sent.
            logger.LogDebug("Request aborted by the client.");
            return true;
        }

        var (status, title, code, retryAfter) = Map(exception);
        httpContext.Response.StatusCode = status;
        if (retryAfter is { } seconds)
        {
            httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { [ProblemResults.CodeExtension] = code },
            },
        });
    }

    private (int Status, string Title, string Code, int? RetryAfterSeconds) Map(Exception exception)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            logger.LogInformation("Malformed request: {Reason}", badRequest.Message);
            return (badRequest.StatusCode, "Invalid request", "request.malformed", null);
        }

        // Business-rule violations raised by the domain (messages are authored by us and safe to show).
        switch (exception)
        {
            case InvalidOrderTransitionException transition:
                logger.LogInformation("Rejected order transition {From} → {To}.", transition.From, transition.To);
                return (StatusCodes.Status409Conflict, transition.Message, transition.Code, null);
            case DomainException domain:
                logger.LogInformation("Domain rule {Code} rejected the request.", domain.Code);
                return (StatusCodes.Status422UnprocessableEntity, domain.Message, domain.Code, null);
            case ConcurrencyConflictException:
                logger.LogInformation("Optimistic concurrency conflict.");
                return (StatusCodes.Status409Conflict, "The resource was modified by another request. Reload and try again.", "conflict.concurrent_update", null);
        }

        var failure = databaseErrors.Classify(exception);
        if (failure is null)
        {
            logger.LogError(exception, "Unhandled exception.");
            return (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.error", null);
        }

        switch (failure.Kind)
        {
            case DatabaseFailureKind.Unavailable:
                logger.LogError(exception, "Database unavailable (provider code {ProviderCode}).", failure.ProviderCode);
                return (StatusCodes.Status503ServiceUnavailable, "The service is temporarily unavailable.", "service.unavailable", 5);
            case DatabaseFailureKind.TransientContention:
                logger.LogWarning(exception, "Database contention (provider code {ProviderCode}).", failure.ProviderCode);
                return (StatusCodes.Status503ServiceUnavailable, "The service is busy. Please retry.", "service.busy", 1);
            case DatabaseFailureKind.ConcurrencyConflict:
                logger.LogInformation("Optimistic concurrency conflict.");
                return (StatusCodes.Status409Conflict, "The resource was modified by another request.", "conflict.concurrent_update", null);
            case DatabaseFailureKind.UniqueViolation:
                logger.LogWarning(exception, "Unique constraint violation (provider code {ProviderCode}).", failure.ProviderCode);
                return (StatusCodes.Status409Conflict, "The resource already exists.", "conflict.duplicate", null);
            case DatabaseFailureKind.ReferenceViolation:
                logger.LogWarning(exception, "Referential integrity violation (provider code {ProviderCode}).", failure.ProviderCode);
                return (StatusCodes.Status409Conflict, "The operation conflicts with related data.", "conflict.reference", null);
            case DatabaseFailureKind.ApplicationRule when failure.ApplicationCode is not null:
                logger.LogWarning(exception, "Database rule {ApplicationCode} rejected the operation.", failure.ApplicationCode);
                return (StatusCodes.Status422UnprocessableEntity, "The operation was rejected by a business rule.", failure.ApplicationCode, null);
            default:
                logger.LogError(exception, "Unclassified database error (provider code {ProviderCode}).", failure.ProviderCode);
                return (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.error", null);
        }
    }
}
