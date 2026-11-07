using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cicek.Infrastructure.Persistence;

/// <summary>
/// The model differs by default schema (runtime user → qualified with the owner schema; migrator → none),
/// so the schema must be part of EF Core's model cache key.
/// </summary>
internal sealed class SchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is AppDbContext app ? (context.GetType(), app.Schema, designTime) : (object)(context.GetType(), designTime);
}
