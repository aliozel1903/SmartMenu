using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddCafeGuvenlikDamgasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuvenlikDamgasi",
                table: "Kafeler",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Mevcut hesaplara rastgele birer damga. Damgasız eski çerezler reddedileceği için
            // bu sürüm yayınlandığında herkes bir kez yeniden giriş yapar.
            migrationBuilder.Sql("UPDATE \"Kafeler\" SET \"GuvenlikDamgasi\" = replace(gen_random_uuid()::text, '-', '');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuvenlikDamgasi",
                table: "Kafeler");
        }
    }
}
