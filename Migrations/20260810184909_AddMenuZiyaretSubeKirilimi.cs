using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuZiyaretSubeKirilimi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuZiyaretleri_CafeId",
                table: "MenuZiyaretleri");

            migrationBuilder.AddColumn<int>(
                name: "SubeId",
                table: "MenuZiyaretleri",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuZiyaretleri_CafeId_SubeId_Tarih",
                table: "MenuZiyaretleri",
                columns: new[] { "CafeId", "SubeId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuZiyaretleri_SubeId",
                table: "MenuZiyaretleri",
                column: "SubeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MenuZiyaretleri_Subeler_SubeId",
                table: "MenuZiyaretleri",
                column: "SubeId",
                principalTable: "Subeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuZiyaretleri_Subeler_SubeId",
                table: "MenuZiyaretleri");

            migrationBuilder.DropIndex(
                name: "IX_MenuZiyaretleri_CafeId_SubeId_Tarih",
                table: "MenuZiyaretleri");

            migrationBuilder.DropIndex(
                name: "IX_MenuZiyaretleri_SubeId",
                table: "MenuZiyaretleri");

            migrationBuilder.DropColumn(
                name: "SubeId",
                table: "MenuZiyaretleri");

            migrationBuilder.CreateIndex(
                name: "IX_MenuZiyaretleri_CafeId",
                table: "MenuZiyaretleri",
                column: "CafeId");
        }
    }
}
