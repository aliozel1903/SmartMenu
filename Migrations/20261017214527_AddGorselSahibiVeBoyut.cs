using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddGorselSahibiVeBoyut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Boyut",
                table: "Gorseller",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CafeId",
                table: "Gorseller",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gorseller_CafeId",
                table: "Gorseller",
                column: "CafeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gorseller_Kafeler_CafeId",
                table: "Gorseller",
                column: "CafeId",
                principalTable: "Kafeler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Bu sürümden önce yüklenen görsellerin boyutunu ve sahibini dolduruyoruz ki kotaya doğru sayılsınlar.
            // Sahip, görselin kullanıldığı yerden bulunur: kafenin logosu ya da bir ürünün resmi.
            migrationBuilder.Sql(@"
                UPDATE ""Gorseller"" SET ""Boyut"" = length(""Veri"");

                UPDATE ""Gorseller"" g SET ""CafeId"" = k.""Id""
                FROM ""Kafeler"" k
                WHERE g.""CafeId"" IS NULL AND k.""LogoUrl"" = '/gorsel/' || g.""Id""::text;

                UPDATE ""Gorseller"" g SET ""CafeId"" = kat.""CafeId""
                FROM ""Urunler"" u JOIN ""Kategoriler"" kat ON kat.""Id"" = u.""KategoriId""
                WHERE g.""CafeId"" IS NULL AND u.""ResimUrl"" = '/gorsel/' || g.""Id""::text;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gorseller_Kafeler_CafeId",
                table: "Gorseller");

            migrationBuilder.DropIndex(
                name: "IX_Gorseller_CafeId",
                table: "Gorseller");

            migrationBuilder.DropColumn(
                name: "Boyut",
                table: "Gorseller");

            migrationBuilder.DropColumn(
                name: "CafeId",
                table: "Gorseller");
        }
    }
}
