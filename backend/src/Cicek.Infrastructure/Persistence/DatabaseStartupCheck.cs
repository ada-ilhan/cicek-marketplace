using Cicek.Application.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Reports database reachability once at startup, in the background. The API keeps running when Oracle is
/// down (liveness stays green, readiness is red) so orchestrators do not enter a restart loop; the log line
/// tells operators exactly what to check.
/// </summary>
internal sealed class DatabaseStartupCheck(
    IServiceScopeFactory scopes,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseStartupCheck> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var probe = scope.ServiceProvider.GetRequiredService<IDatabaseStatusProbe>();
        var status = await probe.GetStatusAsync(stoppingToken);
        var dataSource = options.Value.DataSource;

        if (!status.IsReachable)
        {
            logger.LogError(
                "Oracle database at {DataSource} is unreachable ({OracleError}). The API is running but /health/ready " +
                "will report Unhealthy. Check Database__DataSource, Database__UserId, Database__Password and that the " +
                "database service and listener are running.",
                dataSource,
                status.FailureCode);
            return;
        }

        if (status.PendingMigrations.Count > 0)
        {
            logger.LogWarning(
                "Oracle database at {DataSource} has {PendingCount} pending migration(s): {Pending}. Run the DbMigrator.",
                dataSource,
                status.PendingMigrations.Count,
                string.Join(", ", status.PendingMigrations));
            return;
        }

        logger.LogInformation(
            "Oracle database at {DataSource} reachable; {AppliedCount} migration(s) applied, latest {Latest}.",
            dataSource,
            status.AppliedMigrationCount,
            status.LatestAppliedMigration);
    }
}
