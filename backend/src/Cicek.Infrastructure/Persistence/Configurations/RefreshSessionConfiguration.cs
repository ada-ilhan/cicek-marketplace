using Cicek.Domain.Identity;
using Cicek.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cicek.Infrastructure.Persistence.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        // Oracle EF Core wraps CREATE TABLE in EXECUTE IMMEDIATE '...' without escaping, so string literals in
        // check constraints must be written with doubled quotes (verified against Oracle 21c; see ADR-0009).
        builder.ToTable("IDN_REFRESH_SESSIONS", table =>
            table.HasCheckConstraint(
                "CK_IDN_REFRESH_SESSIONS_REASON",
                OracleCheckConstraints.InList("REVOCATION_REASON", Enum.GetNames<RefreshSessionRevocationReason>(), allowNull: true)));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // Lookup key for every refresh/logout request.
        builder.Property(s => s.TokenHash).HasMaxLength(RefreshSession.TokenHashLength).IsUnicode(false).IsFixedLength().IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();

        builder.Property(s => s.RevocationReason).HasConversion<string>().HasMaxLength(32).IsUnicode(false);
        builder.HasIndex(s => s.FamilyId);
        builder.HasIndex(s => s.UserId);

        // Sessions are security state, not business records: deleting a user removes them.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

        // Two concurrent rotations of the same token must not both succeed.
        builder.Property<long>(AppDbContext.RowVersionProperty).IsConcurrencyToken();
    }
}
