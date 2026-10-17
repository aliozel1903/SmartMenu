using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;

namespace SmartMenu.Services
{
    /// <summary>
    /// Yüklenen görselleri doğrulayıp veritabanına kaydeder.
    ///
    /// Sadece dosya uzantısına bakmak yeterli değildir: uzantı istemciden gelir ve
    /// saldırgan zararlı bir dosyayı "logo.png" adıyla gönderebilir. Bu yüzden burada
    /// dosyanın ilk baytlarındaki gerçek format imzası (magic bytes) da kontrol edilir.
    ///
    /// Görseller diske değil veritabanına yazılır, çünkü bulut sunucularında disk kalıcı değildir.
    /// </summary>
    public class GuvenliResimYukleyici
    {
        private const string GorselYolOneki = "/gorsel/";

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Logo/görsel için 2 MB fazlasıyla yeterli. Sınır olmazsa depolama doldurma saldırısına açık kalırız.
        public const long MaksimumBoyutBayt = 2 * 1024 * 1024;

        private static readonly string[] IzinliUzantilar = { ".jpg", ".jpeg", ".png", ".webp" };

        // Hesap başına kota. Ürün görselleri tarayıcıda 600x600'e küçültüldüğü için (yaklaşık 50-100 KB)
        // normal bir kafe bu sınırlara yaklaşmaz; sınırlar sadece kötüye kullanımı durdurmak için var.
        public const int KafeBasinaMaksimumGorsel = 100;
        public const long KafeBasinaMaksimumBayt = 20L * 1024 * 1024;

        // Tüm sistem için emniyet sınırı. Görseller veritabanında tutulduğu için, biri çok sayıda hesap
        // açıp görsel yüklese bile veritabanı dolmasın: menü ve siparişler çalışmaya devam eder,
        // sadece yeni görsel yüklemesi durur.
        public const long ToplamMaksimumBayt = 300L * 1024 * 1024;

        public GuvenliResimYukleyici(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>
        /// Dosyayı doğrulayıp kaydeder. Başarılıysa görselin adresini (ör. "/gorsel/{id}") döndürür.
        /// </summary>
        /// <param name="cafeId">Görseli yükleyen kafe; kota bu kafeye göre hesaplanır.</param>
        /// <param name="yerineGecenYol">Değiştirilecek mevcut görsel (ör. logoyu yenilerken). Kota hesabında sayılmaz,
        /// çünkü yeni görsel kaydedildikten sonra silinecek.</param>
        public async Task<(bool Basarili, string? Yol, string? Hata)> YukleAsync(IFormFile dosya, int cafeId, string? yerineGecenYol = null)
        {
            if (dosya == null || dosya.Length == 0)
            {
                return (false, null, "Dosya seçilmedi.");
            }

            if (dosya.Length > MaksimumBoyutBayt)
            {
                return (false, null, $"Dosya boyutu en fazla {MaksimumBoyutBayt / (1024 * 1024)} MB olabilir.");
            }

            var uzanti = Path.GetExtension(dosya.FileName).ToLowerInvariant();
            if (!IzinliUzantilar.Contains(uzanti))
            {
                return (false, null, "Sadece JPG, PNG veya WEBP formatında görsel yükleyebilirsiniz.");
            }

            await using var bellek = new MemoryStream();
            await dosya.CopyToAsync(bellek);
            var veri = bellek.ToArray();

            // Asıl kontrol: içerik gerçekten bir görsel mi? İçerik tipini de istemcinin
            // söylediğine göre değil, dosyanın kendi imzasına göre belirliyoruz.
            var icerikTipi = IcerikTipiniBul(veri);
            if (icerikTipi == null)
            {
                return (false, null, "Dosya geçerli bir görsel değil.");
            }

            var kotaHatasi = await KotaHatasiAsync(cafeId, veri.Length, GorselIdsiniCoz(yerineGecenYol));
            if (kotaHatasi != null)
            {
                return (false, null, kotaHatasi);
            }

            var gorsel = new Gorsel
            {
                Id = Guid.NewGuid(),
                IcerikTipi = icerikTipi,
                Veri = veri,
                Boyut = veri.Length,
                CafeId = cafeId,
                OlusturulmaTarihi = DateTime.UtcNow
            };

            _context.Gorseller.Add(gorsel);
            await _context.SaveChangesAsync();

            return (true, GorselYolOneki + gorsel.Id, null);
        }

        /// <summary>
        /// Daha önce yüklenmiş bir görseli siler. Eski sürümden kalma "/img/..." disk yollarını da temizler.
        /// </summary>
        public async Task SilAsync(string? yol)
        {
            if (string.IsNullOrWhiteSpace(yol))
            {
                return;
            }

            if (yol.StartsWith(GorselYolOneki, StringComparison.OrdinalIgnoreCase))
            {
                if (GorselIdsiniCoz(yol) is Guid id)
                {
                    var gorsel = await _context.Gorseller.FindAsync(id);
                    if (gorsel != null)
                    {
                        _context.Gorseller.Remove(gorsel);
                        await _context.SaveChangesAsync();
                    }
                }
                return;
            }

            // Eski kayıtlar diskte duruyor. Yolun gerçekten wwwroot içinde kaldığını doğrulamadan silmiyoruz;
            // aksi halde "../appsettings.json" gibi bir değerle sunucudan dosya sildirilebilirdi.
            var kokDizin = Path.GetFullPath(_env.WebRootPath) + Path.DirectorySeparatorChar;
            var tamYol = Path.GetFullPath(Path.Combine(_env.WebRootPath, yol.TrimStart('/')));
            if (tamYol.StartsWith(kokDizin, StringComparison.Ordinal) && File.Exists(tamYol))
            {
                File.Delete(tamYol);
            }
        }

        private async Task<string?> KotaHatasiAsync(int cafeId, int yeniBoyut, Guid? haricId)
        {
            var kafeninGorselleri = _context.Gorseller.Where(g => g.CafeId == cafeId && g.Id != haricId);

            if (await kafeninGorselleri.CountAsync() >= KafeBasinaMaksimumGorsel)
            {
                return $"En fazla {KafeBasinaMaksimumGorsel} görsel yükleyebilirsiniz. Kullanmadığınız ürün görsellerini silerek yer açabilirsiniz.";
            }

            if (await kafeninGorselleri.SumAsync(g => (long)g.Boyut) + yeniBoyut > KafeBasinaMaksimumBayt)
            {
                return $"Görseller için ayrılan {KafeBasinaMaksimumBayt / (1024 * 1024)} MB alan doldu. Kullanmadığınız ürün görsellerini silerek yer açabilirsiniz.";
            }

            if (await _context.Gorseller.SumAsync(g => (long)g.Boyut) + yeniBoyut > ToplamMaksimumBayt)
            {
                return "Görsel yükleme şu anda geçici olarak kapalı. Lütfen daha sonra tekrar deneyin.";
            }

            return null;
        }

        private static Guid? GorselIdsiniCoz(string? yol)
        {
            if (string.IsNullOrWhiteSpace(yol) || !yol.StartsWith(GorselYolOneki, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Guid.TryParse(yol.Substring(GorselYolOneki.Length), out var id) ? id : null;
        }

        private static string? IcerikTipiniBul(byte[] veri)
        {
            if (veri.Length < 12)
            {
                return null;
            }

            // JPEG: FF D8 FF
            if (veri[0] == 0xFF && veri[1] == 0xD8 && veri[2] == 0xFF)
            {
                return "image/jpeg";
            }

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            byte[] pngImzasi = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (veri.Take(8).SequenceEqual(pngImzasi))
            {
                return "image/png";
            }

            // WEBP: 0-3 "RIFF", 8-11 "WEBP"
            if (System.Text.Encoding.ASCII.GetString(veri, 0, 4) == "RIFF"
                && System.Text.Encoding.ASCII.GetString(veri, 8, 4) == "WEBP")
            {
                return "image/webp";
            }

            return null;
        }
    }
}
