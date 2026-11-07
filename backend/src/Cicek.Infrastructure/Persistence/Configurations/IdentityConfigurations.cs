using Cicek.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cicek.Infrastructure.Persistence.Configurations;

/// <summary>
/// ASP.NET Core Identity tables with explicit Oracle-friendly names and bounded string lengths
/// (unbounded strings would default to NVARCHAR2(2000)). Strings use NVARCHAR2 (character semantics).
/// </summary>
internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("IDN_USERS");
        builder.Property(u => u.UserName).HasMaxLength(256);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(256);
        builder.Property(u => u.Email).HasMaxLength(256);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(256);
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsUnicode(false);
        builder.Property(u => u.SecurityStamp).HasMaxLength(64).IsUnicode(false);
        builder.Property(u => u.ConcurrencyStamp).HasMaxLength(64).IsUnicode(false);
        builder.Property(u => u.PhoneNumber).HasMaxLength(32).IsUnicode(false);
        builder.Property(u => u.CreatedAt).IsRequired();
    }
}

internal sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.ToTable("IDN_ROLES");
        builder.Property(r => r.Name).HasMaxLength(256);
        builder.Property(r => r.NormalizedName).HasMaxLength(256);
        builder.Property(r => r.ConcurrencyStamp).HasMaxLength(64).IsUnicode(false);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("IDN_USER_ROLES");
}

internal sealed class UserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("IDN_USER_CLAIMS");
        builder.Property(c => c.ClaimType).HasMaxLength(256);
        builder.Property(c => c.ClaimValue).HasMaxLength(1024);
    }
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder)
    {
        builder.ToTable("IDN_ROLE_CLAIMS");
        builder.Property(c => c.ClaimType).HasMaxLength(256);
        builder.Property(c => c.ClaimValue).HasMaxLength(1024);
    }
}

internal sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("IDN_USER_LOGINS");
        builder.Property(l => l.LoginProvider).HasMaxLength(128);
        builder.Property(l => l.ProviderKey).HasMaxLength(128);
        builder.Property(l => l.ProviderDisplayName).HasMaxLength(256);
    }
}

internal sealed class UserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("IDN_USER_TOKENS");
        builder.Property(t => t.LoginProvider).HasMaxLength(128);
        builder.Property(t => t.Name).HasMaxLength(128);
        builder.Property(t => t.Value).HasMaxLength(2000);
    }
}
