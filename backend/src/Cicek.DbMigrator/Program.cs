using Cicek.DbMigrator;
using Cicek.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Explicit database deployment tool (ADR-0009). Runs as the schema OWNER, never as the runtime user.
//   migrate               apply pending EF Core migrations, sync runtime grants, seed roles
//   Usage: Cicek.DbMigrator <migrate|status|create-admin --email <address>|seed-demo>                list applied and pending migrations (read-only)
//   create-admin --email  create or promote an administrator; password from CICEK_ADMIN_PASSWORD
//   seed-demo             DEV ONLY: sample florists/products/customer (needs CICEK_ALLOW_DEMO_SEED=true, CICEK_DEMO_PASSWORD)
var command = args.FirstOrDefault()?.ToLowerInvariant();
if (command is not ("migrate" or "status" or "create-admin" or "seed-demo"))
{
    await Console.Error.WriteLineAsync("Usage: Cicek.DbMigrator <migrate|status|create-admin --email <address>>");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args.Skip(1).ToArray(),
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Configuration.AddUserSecrets<MigratorCommands>(optional: true);
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Services.AddPersistence();
builder.Services.AddOptions<MigratorOptions>().BindConfiguration(MigratorOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<Cicek.Application.Common.MarketplaceOptions>().BindConfiguration(Cicek.Application.Common.MarketplaceOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddScoped<MigratorCommands>();
builder.Services.AddScoped<DemoDataSeeder>();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

try
{
    var commands = scope.ServiceProvider.GetRequiredService<MigratorCommands>();
    return command switch
    {
        "migrate" => await commands.MigrateAsync(cts.Token),
        "status" => await commands.StatusAsync(cts.Token),
        "seed-demo" => await commands.SeedDemoAsync(builder.Environment.EnvironmentName, cts.Token),
        _ => await commands.CreateAdminAsync(builder.Configuration["email"], Environment.GetEnvironmentVariable("CICEK_ADMIN_PASSWORD"), cts.Token),
    };
}
catch (OptionsValidationException ex)
{
    await Console.Error.WriteLineAsync($"Invalid configuration: {string.Join("; ", ex.Failures)}");
    await Console.Error.WriteLineAsync("Set Database__DataSource, Database__UserId and Database__Password (schema owner), e.g. via environment variables or user-secrets.");
    return 2;
}
#pragma warning disable CA1031 // Top-level boundary of a CLI tool: report and return a failure exit code.
catch (Exception ex)
#pragma warning restore CA1031
{
    scope.ServiceProvider.GetRequiredService<ILogger<MigratorCommands>>().LogError(ex, "Command '{Command}' failed.", command);
    return 1;
}
