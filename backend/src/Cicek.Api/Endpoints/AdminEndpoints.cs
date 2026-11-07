using Cicek.Api.Auth;
using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Admin;
using Cicek.Application.Authorization;
using Cicek.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Cicek.Api.Endpoints;

/// <summary>Administrator dashboard data. Every endpoint requires an explicit permission (checked on the server).</summary>
internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").WithTags("Admin: Dashboard");

        admin.MapGet("/overview", async (GetAdminOverviewHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct)))
            .RequirePermission(Permissions.AdminOverviewRead)
            .WithName("AdminOverview").WithSummary("Counts and money figures for the dashboard.")
            .Produces<AdminOverviewDto>();

        admin.MapGet("/users", async ([FromQuery(Name = "q")] string? search, string? role, [AsParameters] PagingParameters paging,
                    ListUsersHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(search, role, paging.ToPageRequest(), ct)))
            .RequirePermission(Permissions.UsersRead)
            .WithName("AdminListUsers").WithSummary("Users with roles and lock state (email search, role filter). No passwords, tokens or phone numbers.")
            .Produces<PagedResult<UserListItemDto>>();

        return api;
    }
}
