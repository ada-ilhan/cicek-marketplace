using Cicek.Domain.Common;
using Cicek.SharedKernel.Domain;

namespace Cicek.Domain.Catalog;

/// <summary>
/// Product category (two levels: e.g. "Çiçekler" → "Güller", "Hediyeler" → "Çikolata"). Managed by administrators.
/// Inactive categories stay referenced by existing products but accept no new products.
/// </summary>
public sealed class Category : Entity
{
    public const int NameMaxLength = 100;
    public const int SlugMaxLength = 100;

    private Category(Guid id, string name, string slug, Guid? parentId, int sortOrder)
        : base(id)
    {
        Rename(name);
        Slug = ValidSlug(slug);
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private Category()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    /// <param name="parent">Must itself be a top-level category (two-level hierarchy).</param>
    public static Category Create(string name, string slug, Category? parent, int sortOrder)
    {
        if (parent is not null && parent.ParentId is not null)
        {
            throw new DomainException("category.too_deep", "Categories support two levels only.");
        }

        return new Category(Guid.CreateVersion7(), name, slug, parent?.Id, sortOrder);
    }

    /// <summary>Reference data with a fixed id (used for seeded categories).</summary>
    public static Category Seed(Guid id, string name, string slug, Guid? parentId, int sortOrder) =>
        new(id, name, slug, parentId, sortOrder);

    public void Update(string name, int sortOrder, bool isActive)
    {
        Rename(name);
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    private void Rename(string name) => Name = Guard.RequireText(name, NameMaxLength, "category.name_required");

    private static string ValidSlug(string slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= SlugMaxLength && slug == SearchText.Slugify(slug, SlugMaxLength)
            ? slug
            : throw new DomainException("category.invalid_slug", "Category slug is invalid.");
}
