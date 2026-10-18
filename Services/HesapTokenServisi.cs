using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;

namespace SmartMenu.Services
{
    /// <summary>
    /// E-posta doğrulama ve şifre sıfırlama bağlantılarının token'larını üretir ve doğrular.
    ///
    /// Gönderim sınırları bu sınıfta: kayıt formu herkese açık olduğu için biri başkasının
    /// adresine yüzlerce e-posta attırmaya çalışabilir. Bu hem o kişiyi rahatsız eder hem de
    /// gönderen alan adımızın itibarını düşürüp e-postalarımızın spam'e düşmesine yol açar.
    /// </summary>
    public class HesapTokenServisi
    {
        private readonly ApplicationDbContext _context;

        // Aynı hesaba aynı türde iki e-posta arasında en az bu kadar süre olmalı.
        private static readonly TimeSpan BeklemeSuresi = TimeSpan.FromMinutes(1);

        // Bir hesaba aynı türde günde en fazla bu kadar e-posta gönderilir.
        private const int HesapBasinaGunlukSinir = 5;

        // Resend'in ücretsiz planı günde 100 e-postaya izin veriyor. Sınırı aşıp hesabımızın
        // askıya alınmasındansa, o gün yeni e-posta göndermeyi durdurmayı tercih ediyoruz.
        private const int ToplamGunlukSinir = 90;

        public HesapTokenServisi(ApplicationDbContext context)
        {
            _context = context;
        }

        public static TimeSpan GecerlilikSuresi(EpostaTokenAmaci amac) =>
            amac == EpostaTokenAmaci.SifreSifirlama ? TimeSpan.FromHours(1) : TimeSpan.FromHours(24);

        /// <summary>
        /// Yeni bir token üretip özetini kaydeder ve token'ın kendisini döndürür (e-postadaki bağlantıya bu girer).
        /// Gönderim sınırlarından biri aşıldıysa null döner; bu durumda e-posta gönderilmemelidir.
        /// </summary>
        public async Task<string?> UretAsync(int cafeId, EpostaTokenAmaci amac)
        {
            var simdi = DateTime.UtcNow;
            var birGunOnce = simdi.AddDays(-1);
            var birHaftaOnce = simdi.AddDays(-7);
            var beklemeBaslangici = simdi - BeklemeSuresi;

            // Bir haftadan eski kayıtların artık hiçbir işlevi yok; tabloyu küçük tutalım.
            await _context.EpostaTokenlari
                .Where(t => t.OlusturulmaTarihi < birHaftaOnce)
                .ExecuteDeleteAsync();

            if (await _context.EpostaTokenlari.CountAsync(t => t.OlusturulmaTarihi > birGunOnce) >= ToplamGunlukSinir)
            {
                return null;
            }

            var hesabinSonTokenlari = _context.EpostaTokenlari
                .Where(t => t.CafeId == cafeId && t.Amac == amac && t.OlusturulmaTarihi > birGunOnce);

            if (await hesabinSonTokenlari.CountAsync() >= HesapBasinaGunlukSinir ||
                await hesabinSonTokenlari.AnyAsync(t => t.OlusturulmaTarihi > beklemeBaslangici))
            {
                return null;
            }

            // Yeni bağlantı gönderilince eskileri geçersiz olur; kullanıcının elinde çalışan tek bir bağlantı kalır.
            await _context.EpostaTokenlari
                .Where(t => t.CafeId == cafeId && t.Amac == amac && t.KullanilmaTarihi == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.KullanilmaTarihi, simdi));

            // QR token'larıyla aynı kriptografik üretici: 256 bit, tahmin edilemez.
            var token = QrTokenUretici.Uret();

            _context.EpostaTokenlari.Add(new EpostaToken
            {
                CafeId = cafeId,
                Amac = amac,
                TokenHash = Ozetle(token),
                OlusturulmaTarihi = simdi,
                SonKullanmaTarihi = simdi + GecerlilikSuresi(amac)
            });
            await _context.SaveChangesAsync();

            return token;
        }

        /// <summary>Token geçerliyse (doğru amaç, süresi dolmamış, kullanılmamış) sahibi olan kafeyi döndürür.</summary>
        public async Task<Cafe?> GecerliTokeninKafesiAsync(string? token, EpostaTokenAmaci amac)
        {
            var kayit = await GecerliKaydiBulAsync(token, amac);
            return kayit?.Cafe;
        }

        /// <summary>
        /// Token geçerliyse kullanıldı olarak işaretler ve sahibi olan kafeyi döndürür; geçersizse null.
        /// Değişiklik kaydedilmez: çağıran taraf kafeyi de güncelleyip tek seferde SaveChanges yapar.
        /// </summary>
        public async Task<Cafe?> KullanAsync(string? token, EpostaTokenAmaci amac)
        {
            var kayit = await GecerliKaydiBulAsync(token, amac);
            if (kayit == null)
            {
                return null;
            }

            kayit.KullanilmaTarihi = DateTime.UtcNow;
            return kayit.Cafe;
        }

        private async Task<EpostaToken?> GecerliKaydiBulAsync(string? token, EpostaTokenAmaci amac)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var ozet = Ozetle(token);
            var simdi = DateTime.UtcNow;

            return await _context.EpostaTokenlari
                .Include(t => t.Cafe)
                .FirstOrDefaultAsync(t => t.TokenHash == ozet
                                          && t.Amac == amac
                                          && t.KullanilmaTarihi == null
                                          && t.SonKullanmaTarihi > simdi);
        }

        private static string Ozetle(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
