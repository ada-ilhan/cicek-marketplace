using Cicek.Application.Authorization;
using Cicek.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// The single deployment sequence, shared by the DbMigrator and the Oracle integration tests so that tests
/// exercise exactly what runs in production: apply migrations → sync runtime grants → seed reference roles.
/// Must run as the schema owner.
/// </summary>
public sealed class DatabaseDeployment(
    AppDbContext db,
    SchemaPrivilegeSynchronizer privileges,
    RoleManager<AppRole> roleManager,
    ILogger<DatabaseDeployment> logger)
{
    public async Task<DeploymentResult> DeployAsync(string runtimeRole, CancellationToken cancellationToken)
    {
        if (db.Schema is not null)
        {
            throw new InvalidOperationException("Deployment must connect as the schema owner (Database:Schema empty).");
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        logger.LogInformation("Pending migrations: {Count} {List}", pending.Count, string.Join(", ", pending));

        // Oracle DDL commits implicitly, so a failure part-way through a migration can leave it partially
        // applied. Mitigation: review generated scripts and apply to a clean schema in CI first.
        await db.Database.MigrateAsync(cancellationToken);

        var grantChanges = await privileges.SyncAsync(runtimeRole, cancellationToken);
        logger.LogInformation("Runtime privileges synchronized for {Role}: {Changes} GRANT/REVOKE statement(s).", runtimeRole, grantChanges);

        var seededRoles = new List<string>();
        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new AppRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed role {role}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }

            seededRoles.Add(role);
        }

        return new DeploymentResult(pending, grantChanges, seededRoles);
    }
}

public sealed record DeploymentResult(IReadOnlyList<string> AppliedMigrations, int GrantChanges, IReadOnlyList<string> SeededRoles);
