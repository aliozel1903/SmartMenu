using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMenu.Migrations
{
    /// <inheritdoc />
    public partial class BosQrTokenlariniDuzelt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kayıt sırasında açılan merkez şubeye QR token verilmiyordu (boş metin kalıyordu). İlk kayıt
            // sorunsuz geçiyor, ikinci kayıt benzersiz QR index'ine takılıp yarıda kalıyordu: kafe kaydediliyor,
            // şube kaydedilemiyordu.

            // 1) Bu yarım kayıtları sil. Şubesi olmayan ve e-postası doğrulanmamış hesap, ancak bu hatayla
            //    oluşmuş olabilir; doğrulama e-postası da hiç gitmediği için kimse bu hesaba giremiyor.
            migrationBuilder.Sql("""
                DELETE FROM "Kafeler" k
                WHERE k."EmailDogrulandi" = FALSE
                  AND NOT EXISTS (SELECT 1 FROM "Subeler" s WHERE s."CafeId" = k."Id");
                """);

            // 2) Token'ı boş kalmış şubelere rastgele token ver. gen_random_uuid() kriptografik rastgele
            //    kaynaktan üretir; iki UUID yan yana 64 karakter ve yaklaşık 244 bit rastgelelik eder.
            migrationBuilder.Sql("""
                UPDATE "Subeler"
                SET "QrToken" = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', ''),
                    "QrTokenTarihi" = now()
                WHERE "QrToken" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
