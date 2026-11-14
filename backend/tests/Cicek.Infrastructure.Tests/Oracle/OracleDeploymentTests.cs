using Cicek.Application.Abstractions.Persistence;
using Cicek.Infrastructure.Persistence;
using Cicek.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Tests.Oracle;

/// <summary>Migrations, PL/SQL deployment and least-privilege access against a real Oracle database.</summary>
[Collection(OracleCollection.Name)]
public sealed class OracleDeploymentTests(OracleTestDatabase database)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_apply_to_a_clean_schema_and_PLSQL_package_compiles()
    {
        database.SkipIfUnavailable();
        Assert.NotEmpty(database.Deployment!.AppliedMigrations);

        await using var owner = new OracleConnection(database.RequiredSettings.OwnerConnectionString());
        await owner.OpenAsync(Ct);
        await using var command = owner.CreateCommand();
        command.CommandText = "SELECT object_type || ':' || status FROM user_objects WHERE object_name = 'CICEK_SCHEMA_ACCESS' ORDER BY object_type";
        var statuses = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(Ct))
        {
            while (await reader.ReadAsync(Ct))
            {
                statuses.Add(reader.GetString(0));
            }
        }

        Assert.Equal(["PACKAGE:VALID", "PACKAGE BODY:VALID"], statuses);
    }

    [Fact]
    public async Task Deployment_is_idempotent()
    {
        database.SkipIfUnavailable();
        await using var services = OracleTestDatabase.CreateServices(database.RequiredSettings.OwnerConfiguration());
        await using var scope = services.CreateAsyncScope();

        var second = await scope.ServiceProvider.GetRequiredService<DatabaseDeployment>()
            .DeployAsync(database.RequiredSettings.RuntimeRole, Ct);

        Assert.Empty(second.AppliedMigrations);
        Assert.Equal(0, second.GrantChanges);
        Assert.Empty(second.SeededRoles);
    }

    [Fact]
    public async Task Runtime_role_receives_exactly_the_policy_privileges()
    {
        database.SkipIfUnavailable();
        var privileges = await QueryOwnerAsync(
            "SELECT table_name || ':' || privilege FROM user_tab_privs_made WHERE grantee = :grantee ORDER BY 1",
            database.RequiredSettings.RuntimeRole);

        Assert.Contains("EF_MIGRATIONS_HISTORY:SELECT", privileges);
        Assert.DoesNotContain(privileges, p => p.StartsWith("EF_MIGRATIONS_HISTORY:", StringComparison.Ordinal) && p != "EF_MIGRATIONS_HISTORY:SELECT");
        Assert.Equal(
            ["IDN_USERS:DELETE", "IDN_USERS:INSERT", "IDN_USERS:SELECT", "IDN_USERS:UPDATE"],
            privileges.Where(p => p.StartsWith("IDN_USERS:", StringComparison.Ordinal)));
        Assert.DoesNotContain(privileges, p => p.StartsWith("CICEK_SCHEMA_ACCESS:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Runtime_user_cannot_run_DDL_or_the_deployment_package()
    {
        database.SkipIfUnavailable();
        await using var app = new OracleConnection(database.RequiredSettings.AppConnectionString());
        await app.OpenAsync(Ct);

        await using var ddl = app.CreateCommand();
        ddl.CommandText = "CREATE TABLE should_not_exist (id NUMBER)";
        var ddlError = await Assert.ThrowsAsync<OracleException>(() => ddl.ExecuteNonQueryAsync(Ct));
        Assert.Equal(1031, ddlError.Number); // ORA-01031: insufficient privileges

        await using var call = app.CreateCommand();
        call.CommandText = $"BEGIN {database.RequiredSettings.OwnerUser}.cicek_schema_access.grant_runtime_privileges('X', :n); END;";
        call.Parameters.Add(new OracleParameter("n", OracleDbType.Int32, System.Data.ParameterDirection.Output));
        var callError = await Assert.ThrowsAsync<OracleException>(() => call.ExecuteNonQueryAsync(Ct));
        Assert.Contains(callError.Number, new[] { 6550, 942, 4042 }); // not visible to the runtime user
    }

    [Theory]
    [InlineData("bad name; DROP TABLE x", "schema_access.invalid_grantee")]
    [InlineData("CICEK_NO_SUCH_ROLE_TEST", "schema_access.grantee_not_found")]
    public async Task PLSQL_errors_follow_the_application_error_contract(string grantee, string expectedCode)
    {
        database.SkipIfUnavailable();
        await using var services = OracleTestDatabase.CreateServices(database.RequiredSettings.OwnerConfiguration());
        await using var scope = services.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider.GetRequiredService<SchemaPrivilegeSynchronizer>();

        var exception = await Assert.ThrowsAsync<OracleException>(() => synchronizer.SyncAsync(grantee, Ct));
        var failure = scope.ServiceProvider.GetRequiredService<IDatabaseErrorClassifier>().Classify(exception);

        Assert.Equal(DatabaseFailureKind.ApplicationRule, failure?.Kind);
        Assert.Equal(expectedCode, failure?.ApplicationCode);
    }

    [Fact]
    public async Task Runtime_status_probe_reads_owner_migration_history_through_schema_qualification()
    {
        database.SkipIfUnavailable();
        await using var services = OracleTestDatabase.CreateServices(database.RequiredSettings.AppConfiguration());
        await using var scope = services.CreateAsyncScope();

        var status = await scope.ServiceProvider.GetRequiredService<IDatabaseStatusProbe>().GetStatusAsync(Ct);

        Assert.True(status.IsReachable);
        Assert.Empty(status.PendingMigrations);
        Assert.Equal(scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetMigrations().Count(), status.AppliedMigrationCount);
    }

    [Fact]
    public async Task Unreachable_database_is_classified_as_unavailable_without_details()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:DataSource"] = "localhost:1599/NOWHERE",
            ["Database:UserId"] = "NOBODY",
            ["Database:Password"] = "irrelevant",
            ["Database:ConnectionTimeoutSeconds"] = "3",
            ["Database:MinPoolSize"] = "0",
        };
        await using var services = OracleTestDatabase.CreateServices(settings);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.OpenConnectionAsync(Ct));
        var failure = scope.ServiceProvider.GetRequiredService<IDatabaseErrorClassifier>().Classify(exception);
        var status = await scope.ServiceProvider.GetRequiredService<IDatabaseStatusProbe>().GetStatusAsync(Ct);

        // Same expectation as before; only the failure output is richer so that an unexpected provider error is visible in CI.
        Assert.True(failure?.Kind == DatabaseFailureKind.Unavailable, DescribeUnreachable(exception, failure, status));
        Assert.False(status.IsReachable);
        Assert.StartsWith("ORA-", status.FailureCode, StringComparison.Ordinal);
    }

    /// <summary>
    /// Assertion output for the unreachable-database test: classification, the status probe's error code and the exception chain
    /// (type, ORA number, message). Messages that mention credentials are withheld; the test itself only uses placeholder values.
    /// </summary>
    private static string DescribeUnreachable(Exception exception, DatabaseFailure? failure, DatabaseStatus status)
    {
        var chain = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var type = current is OracleException oracle ? $"{current.GetType().FullName} ORA-{oracle.Number:D5}" : current.GetType().FullName;
            chain.Add($"{type}: {SafeMessage(current.Message)}");
        }

        var providerCode = failure?.ProviderCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        return $"Expected Unavailable but the classifier returned {failure?.Kind.ToString() ?? "null"} (provider code {providerCode}); " +
               $"status probe: reachable={status.IsReachable}, failure={status.FailureCode ?? "none"}. Exception chain: {string.Join(" -> ", chain)}";
    }

    private static string SafeMessage(string message)
    {
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase) || message.Contains("pwd", StringComparison.OrdinalIgnoreCase))
        {
            return "[withheld: message mentions credentials]";
        }

        var singleLine = string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 400 ? singleLine : singleLine[..400];
    }

    private async Task<List<string>> QueryOwnerAsync(string sql, string parameter)
    {
        await using var owner = new OracleConnection(database.RequiredSettings.OwnerConnectionString());
        await owner.OpenAsync(Ct);
        await using var command = owner.CreateCommand();
        command.BindByName = true;
        command.CommandText = sql;
        command.Parameters.Add(new OracleParameter("grantee", parameter));
        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
