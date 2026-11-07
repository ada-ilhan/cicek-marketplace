using System.Data;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Calls <c>cicek_schema_access.grant_runtime_privileges</c> (PL/SQL contract in
/// Persistence/Sql/Packages/cicek_schema_access). Must run as the schema owner and outside any
/// transaction, because GRANT/REVOKE commit implicitly.
/// </summary>
public sealed class SchemaPrivilegeSynchronizer(AppDbContext db)
{
    /// <returns>Number of GRANT/REVOKE statements executed.</returns>
    public async Task<int> SyncAsync(string runtimeRole, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Privilege synchronization issues DDL and must not run inside a transaction.");
        }

        var connection = (OracleConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.BindByName = true;
            command.CommandText = "BEGIN cicek_schema_access.grant_runtime_privileges(p_grantee => :p_grantee, p_changes => :p_changes); END;";
            command.Parameters.Add(new OracleParameter("p_grantee", OracleDbType.Varchar2, runtimeRole, ParameterDirection.Input));
            var changes = command.Parameters.Add(new OracleParameter("p_changes", OracleDbType.Int32, ParameterDirection.Output));
            await command.ExecuteNonQueryAsync(cancellationToken);
            return ((Oracle.ManagedDataAccess.Types.OracleDecimal)changes.Value).ToInt32();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
