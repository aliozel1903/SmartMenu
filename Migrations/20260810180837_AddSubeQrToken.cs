using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class AddSubeQrToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrToken",
                table: "Subeler",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "QrTokenTarihi",
                table: "Subeler",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Mevcut şubeler kolonu boş ("") değerle alır; bu da benzersiz index'i ihlal eder.
            // Bu yüzden index oluşturulmadan ÖNCE her şubeye benzersiz bir token yazıyoruz.
            // Id de karışıma dahil edildiği için iki satırın aynı değeri alması mümkün değil.
            // Not: Buradaki md5 yalnızca ilk dolum içindir; yönetici panelden "QR Kodu Yenile" dediğinde
            // token kriptografik üreteçle (QrTokenUretici) yeniden oluşturulur.
            migrationBuilder.Sql(@"
                UPDATE ""Subeler""
                SET ""QrToken"" = md5(random()::text || clock_timestamp()::text || ""Id""::text),
                    ""QrTokenTarihi"" = now() AT TIME ZONE 'UTC'
                WHERE ""QrToken"" IS NULL OR ""QrToken"" = '';
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Subeler_QrToken",
                table: "Subeler",
                column: "QrToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Subeler_QrToken",
                table: "Subeler");

            migrationBuilder.DropColumn(
                name: "QrToken",
                table: "Subeler");

            migrationBuilder.DropColumn(
                name: "QrTokenTarihi",
                table: "Subeler");
        }
    }
}
