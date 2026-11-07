using Cicek.Api.Auth;
using Cicek.Application.Authorization;
using Cicek.Application.SystemStatus;

namespace Cicek.Api.Endpoints;

internal static class AdminSystemEndpoints
{
    public static RouteGroupBuilder MapAdminSystemEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin/system").WithTags("Admin: System");

        admin.MapGet("/status", async (GetSystemStatusHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(ct)))
            .RequirePermission(Permissions.SystemStatusRead)
            .WithName("GetSystemStatus")
            .WithSummary("Database reachability and migration status for operators.")
            .Produces<SystemStatusDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return api;
    }
}
