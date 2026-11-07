using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductAndShopImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IMAGE_URL",
                table: "SHP_SHOPS",
                type: "VARCHAR2(1000)",
                unicode: false,
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MED_IMAGES",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    SHOP_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    STORAGE_KEY = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    CONTENT_TYPE = table.Column<string>(type: "VARCHAR2(32)", unicode: false, maxLength: 32, nullable: false),
                    SIZE_BYTES = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    UPLOADED_BY_USER_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MED_IMAGES", x => x.ID);
                    table.CheckConstraint("CK_MED_IMAGES_SIZE", "SIZE_BYTES > 0");
                    table.ForeignKey(
                        name: "FK_MED_IMAGES_SHP_SHOPS_SHOP_ID",
                        column: x => x.SHOP_ID,
                        principalTable: "SHP_SHOPS",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MED_IMAGES_SHOP_ID",
                table: "MED_IMAGES",
                column: "SHOP_ID");

            migrationBuilder.CreateIndex(
                name: "UX_MED_IMAGES_STORAGE_KEY",
                table: "MED_IMAGES",
                column: "STORAGE_KEY",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MED_IMAGES");

            migrationBuilder.DropColumn(
                name: "IMAGE_URL",
                table: "SHP_SHOPS");
        }
    }
}
