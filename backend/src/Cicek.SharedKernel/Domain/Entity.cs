namespace Cicek.SharedKernel.Domain;

/// <summary>Base for entities with a Guid identity. Equality is by identity and type.</summary>
public abstract class Entity
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity id must not be empty.", nameof(id));
        }

        Id = id;
    }

    /// <summary>For ORM materialization only.</summary>
    protected Entity()
    {
    }

    public Guid Id { get; private init; }

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
