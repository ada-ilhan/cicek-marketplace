using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cicek.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AzerbaijaniCategoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renames only seeded categories that still carry their original Turkish seed name. A category an administrator
            // already renamed (or whose name differs for any reason) is left untouched, so this migration never overwrites
            // administrator-managed data on a database that has been in use.
            foreach (var (id, turkishSeedName, azerbaijaniName) in SeedRenames)
            {
                migrationBuilder.Sql(
                    $"UPDATE CAT_CATEGORIES SET NAME = N'{azerbaijaniName}' " +
                    $"WHERE ID = HEXTORAW('{Convert.ToHexString(id.ToByteArray())}') AND NAME = N'{turkishSeedName}'");
            }
        }

        private static readonly (Guid Id, string TurkishSeedName, string AzerbaijaniName)[] SeedRenames =
        [
            (new Guid("0199d6a0-0000-7000-8000-000000000001"), "Çiçekler", "Çiçəklər"),
            (new Guid("0199d6a0-0000-7000-8000-000000000002"), "Hediyeler", "Hədiyyələr"),
            (new Guid("0199d6a0-0000-7000-8000-000000000011"), "Güller", "Qızılgüllər"),
            (new Guid("0199d6a0-0000-7000-8000-000000000012"), "Orkideler", "Orxideyalar"),
            (new Guid("0199d6a0-0000-7000-8000-000000000013"), "Buketler", "Buketlər"),
            (new Guid("0199d6a0-0000-7000-8000-000000000014"), "Saksı Çiçekleri", "Qabda çiçəklər"),
            (new Guid("0199d6a0-0000-7000-8000-000000000021"), "Çikolatalar", "Şokoladlar"),
            (new Guid("0199d6a0-0000-7000-8000-000000000022"), "Hediye Sepetleri", "Hədiyyə səbətləri"),
            (new Guid("0199d6a0-0000-7000-8000-000000000023"), "Peluş Oyuncaklar", "Yumşaq oyuncaqlar"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000001"),
                column: "NAME",
                value: "Çiçekler");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000002"),
                column: "NAME",
                value: "Hediyeler");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000011"),
                column: "NAME",
                value: "Güller");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000012"),
                column: "NAME",
                value: "Orkideler");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000013"),
                column: "NAME",
                value: "Buketler");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000014"),
                column: "NAME",
                value: "Saksı Çiçekleri");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000021"),
                column: "NAME",
                value: "Çikolatalar");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000022"),
                column: "NAME",
                value: "Hediye Sepetleri");

            migrationBuilder.UpdateData(
                table: "CAT_CATEGORIES",
                keyColumn: "ID",
                keyValue: new Guid("0199d6a0-0000-7000-8000-000000000023"),
                column: "NAME",
                value: "Peluş Oyuncaklar");
        }
    }
}
