using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class SiparisSubeSilinmesinKoru : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Siparisler_Subeler_SubeId",
                table: "Siparisler");

            migrationBuilder.AddForeignKey(
                name: "FK_Siparisler_Subeler_SubeId",
                table: "Siparisler",
                column: "SubeId",
                principalTable: "Subeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Siparisler_Subeler_SubeId",
                table: "Siparisler");

            migrationBuilder.AddForeignKey(
                name: "FK_Siparisler_Subeler_SubeId",
                table: "Siparisler",
                column: "SubeId",
                principalTable: "Subeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
