using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddSubeFranchise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Tarih",
                table: "MenuZiyaretleri",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateTable(
                name: "Subeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CafeId = table.Column<int>(type: "integer", nullable: false),
                    SubeAdi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Telefon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Adres = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subeler_Kafeler_CafeId",
                        column: x => x.CafeId,
                        principalTable: "Kafeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subeler_CafeId",
                    table: "Subeler",
                    column: "CafeId");

            // Mevcut markalar sistemde kaybolmasın diye her Cafe için başlangıçta bir merkez şube oluşturuyoruz.
            migrationBuilder.Sql("""
                INSERT INTO "Subeler" ("CafeId", "SubeAdi", "Telefon", "Adres", "OlusturulmaTarihi")
                SELECT "Id", COALESCE("Ad", 'Merkez Şube'), NULL, "Adres", NOW()
                FROM "Kafeler";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subeler");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Tarih",
                table: "MenuZiyaretleri",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");
        }
    }
}
