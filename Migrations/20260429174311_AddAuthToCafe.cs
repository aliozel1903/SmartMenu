using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthToCafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Kafeler",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SifreHash",
                table: "Kafeler",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Kafeler");

            migrationBuilder.DropColumn(
                name: "SifreHash",
                table: "Kafeler");
        }
    }
}
