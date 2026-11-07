using Cicek.Application.Abstractions.Identity;
using Cicek.Application.Abstractions.Media;
using Cicek.Application.Abstractions.Persistence;
using Cicek.Application.Auth;
using Cicek.Application.Common;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Media;
using Cicek.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Cicek.Infrastructure;

public static class DependencyInjection
{
    public const string ReadinessTag = "ready";

    /// <summary>Oracle persistence + identity stores. Used by both the API and the DbMigrator.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => new DatabaseSchema(sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.NormalizedSchema));

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            ConfigureOracle(options, OracleConnectionStrings.Build(db), db.NormalizedSchema, db.CommandTimeoutSeconds);
        });

        services.AddSingleton<IDatabaseErrorClassifier, OracleDatabaseErrorClassifier>();
        services.AddScoped<IDatabaseStatusProbe, DatabaseStatusProbe>();
        services.TryAddTimeProvider();

        services.AddIdentityCore<AppUser>(options =>
            {
                // NIST SP 800-63B: length, not composition rules. Upper bound enforced by validators.
                options.Password.RequiredLength = AuthLimits.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 4;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;

                // Usernames are email addresses, which are validated separately; allow any character.
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddScoped<IUserAccounts, IdentityUserAccounts>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IRefreshSessionStore, RefreshSessionStore>();
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IProductStock, ProductStock>();

        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(o => o.Provider is "None" or "Local", "Media:Provider must be 'None' or 'Local'.")
            .Validate(o => o.Provider != "Local" || !string.IsNullOrWhiteSpace(o.LocalPath), "Media:LocalPath is required for the Local provider.")
            .ValidateOnStart();
        services.AddSingleton<IImageStorage>(sp =>
            sp.GetRequiredService<IOptions<MediaOptions>>().Value.Provider == "Local"
                ? ActivatorUtilities.CreateInstance<LocalFileImageStorage>(sp)
                : new UnconfiguredImageStorage());
        services.AddScoped<SchemaPrivilegeSynchronizer>();
        services.AddScoped<DatabaseDeployment>();

        return services;
    }

    /// <summary>Runtime-only additions: readiness health check, startup connectivity report, unpaid-order expiry.</summary>
    public static IServiceCollection AddPersistenceDiagnostics(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("oracle", HealthStatus.Unhealthy, [ReadinessTag], TimeSpan.FromSeconds(10));
        services.AddHostedService<DatabaseStartupCheck>();
        services.AddHostedService<BackgroundJobs.OrderExpiryWorker>();
        return services;
    }

    /// <summary>
    /// Single place for Oracle provider settings so runtime, migrator, design-time tooling and tests agree.
    /// SQL compatibility is pinned to 19c (the oldest supported release) so generated SQL and column types
    /// (e.g. NUMBER(1) for bool rather than the 23ai BOOLEAN type) are identical across 19c/21c/23ai/26ai.
    /// </summary>
    internal static void ConfigureOracle(DbContextOptionsBuilder options, string connectionString, string? schema, int commandTimeoutSeconds)
    {
        options.UseOracle(connectionString, oracle =>
        {
            oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
            oracle.MigrationsHistoryTable("EF_MIGRATIONS_HISTORY", schema);
            oracle.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
            oracle.CommandTimeout(commandTimeoutSeconds);

            // No automatic retrying execution strategy: a retried unit of work might already have committed.
            // Retries are decided per use case together with idempotency keys (docs/oracle-database.md).
        });
        options.ReplaceService<IModelCacheKeyFactory, SchemaAwareModelCacheKeyFactory>();
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
