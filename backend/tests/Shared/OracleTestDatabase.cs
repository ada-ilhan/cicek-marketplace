using Cicek.Infrastructure;
using Cicek.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

namespace Cicek.Tests.Shared;

/// <summary>
/// Connection settings for the dedicated Oracle TEST schema pair (created with db/bootstrap). Read from
/// user-secrets id "cicek-tests-dev" or environment variables (TestDatabase__OwnerPassword, …).
/// </summary>
public sealed record OracleTestSettings(
    string DataSource,
    string OwnerUser,
    string OwnerPassword,
    string AppUser,
    string AppPassword,
    string RuntimeRole)
{
    public static OracleTestSettings? TryLoad()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets("cicek-tests-dev")
            .AddEnvironmentVariables()
            .Build()
            .GetSection("TestDatabase");

        var ownerPassword = config["OwnerPassword"];
        var appPassword = config["AppPassword"];
        if (string.IsNullOrEmpty(ownerPassword) || string.IsNullOrEmpty(appPassword))
        {
            return null;
        }

        return new OracleTestSettings(
            config["DataSource"] ?? "localhost:1521/XEPDB1",
            config["OwnerUser"] ?? "CICEK_TEST_OWNER",
            ownerPassword,
            config["AppUser"] ?? "CICEK_TEST_APP",
            appPassword,
            config["RuntimeRole"] ?? "CICEK_TEST_APP_ROLE");
    }

    /// <summary>Configuration keys for the schema owner (deployment) connection.</summary>
    public Dictionary<string, string?> OwnerConfiguration() => new()
    {
        ["Database:DataSource"] = DataSource,
        ["Database:UserId"] = OwnerUser,
        ["Database:Password"] = OwnerPassword,
        ["Database:Schema"] = string.Empty,
        ["Database:MinPoolSize"] = "0",
    };

    /// <summary>Configuration keys for the least-privilege runtime connection (as the API uses it).</summary>
    public Dictionary<string, string?> AppConfiguration() => new()
    {
        ["Database:DataSource"] = DataSource,
        ["Database:UserId"] = AppUser,
        ["Database:Password"] = AppPassword,
        ["Database:Schema"] = OwnerUser,
        ["Database:MinPoolSize"] = "0",
    };

    public string OwnerConnectionString() => OracleConnectionStrings.Build(new DatabaseOptions
    {
        DataSource = DataSource,
        UserId = OwnerUser,
        Password = OwnerPassword,
        MinPoolSize = 0,
    });

    public string AppConnectionString() => OracleConnectionStrings.Build(new DatabaseOptions
    {
        DataSource = DataSource,
        UserId = AppUser,
        Password = AppPassword,
        MinPoolSize = 0,
    });
}

/// <summary>
/// Assembly-wide fixture: resets the TEST schema, then runs the production deployment sequence
/// (<see cref="DatabaseDeployment"/>) against it. A cross-process file lock serialises test assemblies
/// that share the schema. When no test database is configured, tests call <see cref="SkipIfUnavailable"/>.
/// </summary>
public sealed class OracleTestDatabase : IAsyncLifetime
{
    private FileStream? _lock;

    public OracleTestSettings? Settings { get; private set; }

    public DeploymentResult? Deployment { get; private set; }

    public string? UnavailableReason { get; private set; }

    public void SkipIfUnavailable()
    {
        if (UnavailableReason is not null)
        {
            Assert.Skip(UnavailableReason);
        }
    }

    public OracleTestSettings RequiredSettings => Settings ?? throw new InvalidOperationException(UnavailableReason);

    public async ValueTask InitializeAsync()
    {
        Settings = OracleTestSettings.TryLoad();
        if (Settings is null)
        {
            UnavailableReason = "Oracle test database not configured (set TestDatabase:OwnerPassword and TestDatabase:AppPassword; see README).";
            return;
        }

        if (!Settings.OwnerUser.Contains("_TEST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to reset schema '{Settings.OwnerUser}': test schemas must contain '_TEST'.");
        }

        _lock = await AcquireLockAsync();
        await ResetSchemaAsync(Settings);
        OracleConnection.ClearAllPools();

        await using var services = CreateServices(Settings.OwnerConfiguration());
        await using var scope = services.CreateAsyncScope();
        Deployment = await scope.ServiceProvider.GetRequiredService<DatabaseDeployment>()
            .DeployAsync(Settings.RuntimeRole, CancellationToken.None);
    }

    public ValueTask DisposeAsync()
    {
        OracleConnection.ClearAllPools();
        _lock?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Service provider with the real persistence registrations for the given database configuration.</summary>
    public static ServiceProvider CreateServices(Dictionary<string, string?> databaseConfiguration, Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(databaseConfiguration).Build();
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPersistence();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Drops every object owned by the TEST schema. The PL/SQL block re-checks the session user so that it can
    /// never run against a non-test schema even if called with the wrong credentials.
    /// </summary>
    private static async Task ResetSchemaAsync(OracleTestSettings settings)
    {
        const string resetBlock = """
            BEGIN
              IF USER NOT LIKE '%\_TEST\_%' ESCAPE '\' THEN
                RAISE_APPLICATION_ERROR(-20999, 'tests.refused: not a test schema');
              END IF;
              FOR t IN (SELECT table_name FROM user_tables
                         WHERE dropped = 'NO' AND nested = 'NO' AND secondary = 'N'
                           AND (iot_type IS NULL OR iot_type = 'IOT')) LOOP
                EXECUTE IMMEDIATE 'DROP TABLE ' || DBMS_ASSERT.ENQUOTE_NAME(t.table_name, FALSE) || ' CASCADE CONSTRAINTS PURGE';
              END LOOP;
              FOR o IN (SELECT object_name, object_type FROM user_objects
                         WHERE object_type IN ('PACKAGE', 'PROCEDURE', 'FUNCTION', 'VIEW', 'SEQUENCE', 'SYNONYM')
                           AND object_name NOT LIKE 'ISEQ$$%') LOOP
                EXECUTE IMMEDIATE 'DROP ' || o.object_type || ' ' || DBMS_ASSERT.ENQUOTE_NAME(o.object_name, FALSE);
              END LOOP;
              EXECUTE IMMEDIATE 'PURGE RECYCLEBIN';
            END;
            """;

        await using var connection = new OracleConnection(settings.OwnerConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = resetBlock.Replace("\r\n", "\n", StringComparison.Ordinal);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<FileStream> AcquireLockAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), "cicek-oracle-test-schema.lock");
        var deadline = DateTime.UtcNow.AddMinutes(10);
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(500);
            }
        }
    }
}

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleTestDatabase>
{
    public const string Name = "Oracle";
}
