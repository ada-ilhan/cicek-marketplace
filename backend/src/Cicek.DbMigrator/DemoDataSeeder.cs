using Cicek.Application.Authorization;
using Cicek.Application.Common;
using Cicek.Domain.Catalog;
using Cicek.Domain.Shops;
using Cicek.Infrastructure.Identity;
using Cicek.Infrastructure.Persistence;
using Cicek.SharedKernel.Monetary;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cicek.DbMigrator;

/// <summary>
/// DEVELOPMENT/TEST ONLY: creates sample florists, products and a customer so the marketplace can be evaluated locally.
/// It is refused unless <c>CICEK_ALLOW_DEMO_SEED=true</c> is set explicitly, refused in a Production environment, takes
/// every password from <c>CICEK_DEMO_PASSWORD</c> (never hard-coded), and is idempotent (existing demo users and
/// shops are left untouched). Demo accounts use the reserved <c>@example.test</c> domain. Products have no images, so the
/// storefront shows its placeholder; no real photos or business data are fabricated.
/// </summary>
internal sealed class DemoDataSeeder(
    AppDbContext db,
    UserManager<AppUser> users,
    IOptions<MarketplaceOptions> marketplace,
    TimeProvider time,
    ILogger<DemoDataSeeder> logger)
{
    public const string AllowVariable = "CICEK_ALLOW_DEMO_SEED";
    public const string PasswordVariable = "CICEK_DEMO_PASSWORD";
    public const string CustomerEmail = "demo.customer@example.test";

    private static readonly (string Email, string Slug, string Name, string Description, string District, (string Category, string Name, string Description, decimal Price, int Stock)[] Products)[] Florists =
    [
        ("demo.florist1@example.test", "demo-baki-gul-evi", "Bakı Gül Evi (demo)", "Təzə güllər və buketlər. Demo mağaza.", "Nəsimi",
            [("guller", "Qırmızı gül buketi (11 ədəd)", "Seçilmiş qırmızı güllər.", 45m, 12),
             ("guller", "Ağ gül buketi (9 ədəd)", "Zərif ağ güllər.", 38m, 8),
             ("buketler", "Qarışıq mövsüm buketi", "Mövsümün çiçəklərindən.", 55m, 6),
             ("cikolatalar", "Çiçək və şokolad dəsti", "Buket və şokolad.", 72m, 4)]),
        ("demo.florist2@example.test", "demo-nargiz-cicek", "Nərgiz Çiçək Mağazası (demo)", "Orxideya və otaq bitkiləri. Demo mağaza.", "Yasamal",
            [("orkideler", "Ağ orxideya (saksıda)", "Bir budaqlı ağ orxideya.", 65m, 5),
             ("saksi-cicekleri", "Sukkulent kolleksiyası", "Üç kiçik sukkulent.", 28m, 10),
             ("buketler", "Lalə buketi (15 ədəd)", "Rəngarəng lalələr.", 42m, 0)]),
        ("demo.florist3@example.test", "demo-xazar-buket", "Xəzər Buket (demo)", "Hədiyyə səbətləri və buketlər. Demo mağaza.", "Səbail",
            [("hediye-sepetleri", "Hədiyyə səbəti (kiçik)", "Çiçək, şokolad və kart.", 80m, 5),
             ("pelus-oyuncaklar", "Pelüş ayı (40 sm)", "Yumşaq oyuncaq.", 25m, 15)]),
    ];

    public async Task<int> SeedAsync(string password, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var currency = marketplace.Value.MarketCurrency;
        var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, cancellationToken);
        var created = 0;

        await EnsureUserAsync(CustomerEmail, Roles.Customer, password, now, cancellationToken);
        foreach (var florist in Florists)
        {
            var owner = await EnsureUserAsync(florist.Email, Roles.FloristOwner, password, now, cancellationToken);
            if (await db.Shops.AnyAsync(s => s.Slug == florist.Slug, cancellationToken))
            {
                continue;
            }

            var shop = Shop.Apply(
                owner.Id,
                florist.Slug,
                new ShopProfile(florist.Name, florist.Description, florist.Email, "+994 12 000 00 00", "Nümunə küçəsi 1", florist.District, "Bakı", "AZ1000"),
                now);
            shop.Approve(now);
            db.Shops.Add(shop);

            foreach (var product in florist.Products)
            {
                db.Products.Add(Product.Create(shop.Id, categories[product.Category], product.Name, product.Description, new Money(product.Price, currency), product.Stock, now));
            }

            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Demo data ready: {Created} new demo shop(s). Customer {Customer}; florists demo.florist1..3@example.test.", created, CustomerEmail);
        return created;
    }

    private async Task<AppUser> EnsureUserAsync(string email, string role, string password, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, CreatedAt = now };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Could not create demo user {email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }

        var roleResult = await users.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not assign role {role} to {email}: run `migrate` first to seed roles.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return user;
    }
}
