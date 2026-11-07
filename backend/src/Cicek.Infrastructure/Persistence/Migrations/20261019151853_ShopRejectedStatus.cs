using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the 'Rejected' shop status (administrator declines an application). Raw SQL on purpose: the Oracle provider
    /// emits AddCheckConstraint with doubled quotes, which is only valid inside its EXECUTE IMMEDIATE wrapper used for
    /// CREATE TABLE and is a syntax error in a plain ALTER TABLE (ADR-0009). Data is not touched.
    /// </summary>
    /// <inheritdoc />
    public partial class ShopRejectedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE SHP_SHOPS DROP CONSTRAINT CK_SHP_SHOPS_STATUS");
            migrationBuilder.Sql("ALTER TABLE SHP_SHOPS ADD CONSTRAINT CK_SHP_SHOPS_STATUS CHECK (STATUS IN ('PendingApproval', 'Active', 'Inactive', 'Suspended', 'Rejected'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fails (ORA-02293) if rejected shops exist: roll forward instead of deleting data.
            migrationBuilder.Sql("ALTER TABLE SHP_SHOPS DROP CONSTRAINT CK_SHP_SHOPS_STATUS");
            migrationBuilder.Sql("ALTER TABLE SHP_SHOPS ADD CONSTRAINT CK_SHP_SHOPS_STATUS CHECK (STATUS IN ('PendingApproval', 'Active', 'Inactive', 'Suspended'))");
        }
    }
}
