using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef migrations add/script</c>. Generating migrations and scripts needs no live
/// database, so the connection string is a non-functional placeholder. Migrations are applied with the
/// DbMigrator (never <c>dotnet ef database update</c>), so that runtime grants are synchronized afterwards.
/// The model is built without a default schema, i.e. as seen by the schema owner.
/// </summary>
internal sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureOracle(builder, "User Id=design_time;Password=unused;Data Source=localhost:1/NONE", schema: null, commandTimeoutSeconds: 30);
        return new AppDbContext(builder.Options, DatabaseSchema.Owner);
    }
}
