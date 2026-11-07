using Cicek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Cicek.Infrastructure.Tests;

public sealed class MigrationSafetyTests
{
    [Fact]
    public void Standalone_AddCheckConstraint_operations_are_not_used()
    {
        // The Oracle provider emits ALTER TABLE ... CHECK (... ''X'' ...) verbatim; doubled quotes are only valid inside
        // CREATE TABLE's EXECUTE IMMEDIATE wrapper. Standalone check-constraint changes must use migrationBuilder.Sql
        // (found while adding the 'Rejected' shop status; ADR-0009).
        var offenders = typeof(AppDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (Migration)Activator.CreateInstance(t)!)
            .SelectMany(m => m.UpOperations.Concat(m.DownOperations).OfType<AddCheckConstraintOperation>().Select(o => $"{m.GetType().Name}: {o.Name}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Seed_data_migrations_never_overwrite_rows_unconditionally()
    {
        // UpdateData(keyValue, value) replaces whatever an administrator may have saved since the row was seeded.
        // Renaming seeded rows must be a guarded UPDATE (see AzerbaijaniCategoryNames).
        var migration = new Cicek.Infrastructure.Persistence.Migrations.AzerbaijaniCategoryNames();

        Assert.Empty(migration.UpOperations.OfType<UpdateDataOperation>());
        Assert.All(migration.UpOperations.OfType<SqlOperation>(), o => Assert.Contains(" AND NAME = ", o.Sql, StringComparison.Ordinal));
    }
}
