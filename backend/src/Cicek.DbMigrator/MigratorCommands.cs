using Cicek.Application.Auth;
using Cicek.Application.Authorization;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicek.DbMigrator;

internal sealed class MigratorCommands(
    AppDbContext db,
    DatabaseDeployment deployment,
    DemoDataSeeder demoSeeder,
    UserManager<AppUser> userManager,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<MigratorOptions> migratorOptions,
    TimeProvider timeProvider,
    ILogger<MigratorCommands> logger)
{
    public async Task<int> MigrateAsync(CancellationToken cancellationToken)
    {
        if (!EnsureOwnerConnection())
        {
            return 1;
        }

        logger.LogInformation("Target {DataSource} as {User}.", databaseOptions.Value.DataSource, databaseOptions.Value.UserId);
        var result = await deployment.DeployAsync(migratorOptions.Value.RuntimeRole, cancellationToken);
        logger.LogInformation(
            "Migration complete: {Migrations} migration(s) applied, {Grants} grant change(s), roles seeded: {Roles}.",
            result.AppliedMigrations.Count,
            result.GrantChanges,
            result.SeededRoles.Count == 0 ? "none" : string.Join(", ", result.SeededRoles));
        return 0;
    }

    public async Task<int> SeedDemoAsync(string environmentName, CancellationToken cancellationToken)
    {
        if (!EnsureOwnerConnection())
        {
            return 1;
        }

        if (Environment.GetEnvironmentVariable(DemoDataSeeder.AllowVariable) != "true" || string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Demo seeding is refused: set {Variable}=true and run with DOTNET_ENVIRONMENT=Development (never in production).", DemoDataSeeder.AllowVariable);
            return 2;
        }

        var password = Environment.GetEnvironmentVariable(DemoDataSeeder.PasswordVariable);
        if (string.IsNullOrEmpty(password) || password.Length < AuthLimits.PasswordMinLength)
        {
            logger.LogError("Set {Variable} (min {Length} characters). Demo passwords are never hard-coded.", DemoDataSeeder.PasswordVariable, AuthLimits.PasswordMinLength);
            return 2;
        }

        await demoSeeder.SeedAsync(password, cancellationToken);
        return 0;
    }

    public async Task<int> StatusAsync(CancellationToken cancellationToken)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        logger.LogInformation("Applied ({Count}): {List}", applied.Count, string.Join(", ", applied));
        logger.LogInformation("Pending ({Count}): {List}", pending.Count, string.Join(", ", pending));
        return 0;
    }

    public async Task<int> CreateAdminAsync(string? email, string? password, CancellationToken cancellationToken)
    {
        if (!EnsureOwnerConnection())
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogError("Pass --email <address>.");
            return 2;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            if (string.IsNullOrEmpty(password) || password.Length < AuthLimits.PasswordMinLength)
            {
                logger.LogError("Set CICEK_ADMIN_PASSWORD (min {Length} characters). Passwords are not accepted as arguments.", AuthLimits.PasswordMinLength);
                return 2;
            }

            existing = new AppUser { UserName = email.Trim(), Email = email.Trim(), CreatedAt = timeProvider.GetUtcNow() };
            EnsureSucceeded(await userManager.CreateAsync(existing, password), "create administrator");
            logger.LogInformation("Created user {Email}.", email);
        }

        if (!await userManager.IsInRoleAsync(existing, Roles.Administrator))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(existing, Roles.Administrator), "assign Administrator role");
        }

        logger.LogInformation("{Email} is an administrator.", email);
        return 0;
    }

    private bool EnsureOwnerConnection()
    {
        if (databaseOptions.Value.NormalizedSchema is null)
        {
            return true;
        }

        logger.LogError(
            "Database:Schema is set ({Schema}), which indicates runtime-user configuration. The migrator must connect as the schema owner with Database:Schema empty.",
            databaseOptions.Value.Schema);
        return false;
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
