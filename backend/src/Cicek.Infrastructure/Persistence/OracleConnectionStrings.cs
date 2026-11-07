using Oracle.ManagedDataAccess.Client;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Builds ODP.NET connection strings from structured options. Using the builder (instead of string
/// concatenation) prevents configuration values from injecting extra connection attributes.
/// </summary>
public static class OracleConnectionStrings
{
    public static string Build(DatabaseOptions options)
    {
        var builder = new OracleConnectionStringBuilder
        {
            DataSource = options.DataSource,
            UserID = options.UserId,
            Password = options.Password,
            Pooling = true,
            MinPoolSize = options.MinPoolSize,
            MaxPoolSize = options.MaxPoolSize,
            ConnectionTimeout = options.ConnectionTimeoutSeconds,
            ConnectionLifeTime = options.ConnectionLifetimeSeconds,

            // Validating on every checkout costs a round trip per request; dead connections are instead
            // surfaced as ORA-03113/03135 errors, classified as "Unavailable" and evicted by ODP.NET.
            ValidateConnection = false,

            // Keep credentials out of OracleConnection.ConnectionString after open.
            PersistSecurityInfo = false,
        };

        return builder.ConnectionString;
    }
}
