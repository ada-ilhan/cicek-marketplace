using Cicek.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Read-only connectivity and migration status. Catches Oracle errors deliberately: the caller wants a
/// status report, not an exception. Errors are logged with their ORA code (never the connection string).
/// </summary>
internal sealed class DatabaseStatusProbe(AppDbContext db, ILogger<DatabaseStatusProbe> logger) : IDatabaseStatusProbe
{
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PingAsync(cancellationToken);
            return true;
        }
        catch (OracleException ex)
        {
            logger.LogWarning("Oracle connectivity check failed with {OracleError}.", OracleDatabaseErrorClassifier.FormatCode(ex.Number));
            return false;
        }
    }

    public async Task<DatabaseStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PingAsync(cancellationToken);
            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var pending = db.Database.GetMigrations().Except(applied, StringComparer.Ordinal).ToList();
            return new DatabaseStatus(true, applied.Count, pending, applied.LastOrDefault(), null);
        }
        catch (OracleException ex)
        {
            var code = OracleDatabaseErrorClassifier.FormatCode(ex.Number);
            logger.LogWarning("Oracle status check failed with {OracleError}.", code);
            return new DatabaseStatus(false, null, [], null, code);
        }
    }

    private async Task PingAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var opened = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM DUAL";
            command.CommandTimeout = 5;
            await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }
}
