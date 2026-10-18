using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddEpostaDogrulamaVeSifreSifirlama : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailDogrulandi",
                table: "Kafeler",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Bu özellikten önce açılmış hesaplar doğrulanmış sayılır; aksi halde mevcut kullanıcılar
            // bir sonraki girişte kilitli kalırdı. Doğrulama sadece bundan sonraki kayıtlar için zorunlu.
            migrationBuilder.Sql("UPDATE \"Kafeler\" SET \"EmailDogrulandi\" = TRUE;");

            migrationBuilder.CreateTable(
                name: "EpostaTokenlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CafeId = table.Column<int>(type: "integer", nullable: false),
                    Amac = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SonKullanmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    KullanilmaTarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EpostaTokenlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EpostaTokenlari_Kafeler_CafeId",
                        column: x => x.CafeId,
                        principalTable: "Kafeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EpostaTokenlari_CafeId_Amac_OlusturulmaTarihi",
                table: "EpostaTokenlari",
                columns: new[] { "CafeId", "Amac", "OlusturulmaTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_EpostaTokenlari_OlusturulmaTarihi",
                table: "EpostaTokenlari",
                column: "OlusturulmaTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_EpostaTokenlari_TokenHash",
                table: "EpostaTokenlari",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EpostaTokenlari");

            migrationBuilder.DropColumn(
                name: "EmailDogrulandi",
                table: "Kafeler");
        }
    }
}
