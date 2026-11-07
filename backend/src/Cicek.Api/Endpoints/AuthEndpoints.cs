using Cicek.Api.Auth;
using Cicek.Api.ErrorHandling;
using Cicek.Api.RateLimiting;
using Cicek.Application.Auth;

namespace Cicek.Api.Endpoints;

/// <summary>
/// Token endpoints consumed by the Next.js BFF (ADR-0004). Responses contain tokens and are never cached.
/// Email verification and password reset arrive with the notifications module (Stage 3).
/// </summary>
internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });

        auth.MapPost("/register", async (RegisterCustomerCommand command, RegisterCustomerHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(command, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .Validate<RegisterCustomerCommand>()
            .WithName("RegisterCustomer")
            .WithSummary("Register a customer account and start a session.")
            .Produces<AuthSessionDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        auth.MapPost("/login", async (LoginCommand command, LoginHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(command, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .Validate<LoginCommand>()
            .WithName("Login")
            .WithSummary("Authenticate with email and password.")
            .Produces<AuthSessionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/refresh", async (RefreshTokensCommand command, RefreshTokensHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(command, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .Validate<RefreshTokensCommand>()
            .WithName("RefreshTokens")
            .WithSummary("Rotate a refresh token and issue a new access token.")
            .Produces<AuthSessionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("/logout", async (LogoutCommand command, LogoutHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(command, ct);
                return Results.NoContent();
            })
            .Validate<LogoutCommand>()
            .WithName("Logout")
            .WithSummary("Revoke the session family of the given refresh token.")
            .Produces(StatusCodes.Status204NoContent);

        return api;
    }

    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (HttpContext http, GetCurrentUserHandler handler, CancellationToken ct) =>
            {
                if (http.User.GetUserId() is not { } userId)
                {
                    return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");
                }

                var result = await handler.HandleAsync(userId, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
            })
            .RequireAuthorization()
            .WithTags("Account")
            .WithName("GetCurrentUser")
            .WithSummary("The authenticated user, roles and effective permissions.")
            .Produces<CurrentUserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }
}
