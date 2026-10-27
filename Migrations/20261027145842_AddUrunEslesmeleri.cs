using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddUrunEslesmeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UrunEslesmeleri",
                columns: table => new
                {
                    UrunId = table.Column<int>(type: "integer", nullable: false),
                    OnerilenUrunId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UrunEslesmeleri", x => new { x.UrunId, x.OnerilenUrunId });
                    table.ForeignKey(
                        name: "FK_UrunEslesmeleri_Urunler_OnerilenUrunId",
                        column: x => x.OnerilenUrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UrunEslesmeleri_Urunler_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Urunler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UrunEslesmeleri_OnerilenUrunId",
                table: "UrunEslesmeleri",
                column: "OnerilenUrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UrunEslesmeleri");
        }
    }
}
