using System;
using Microsoft.Extensions.Caching.Memory;

namespace SmartMenu.Services
{
    /// <summary>
    /// Aynı e-posta adresine üst üste hatalı şifre girilirse o adresle girişi bir süre kapatır.
    ///
    /// IP başına hız sınırı (bkz. Program.cs, "giris") tek bir bilgisayardan yapılan denemeleri yavaşlatır,
    /// ama çok sayıda farklı IP'den tek bir hesaba saldıran birini durdurmaz. Bu sınıf onu hesap
    /// tarafından keser.
    ///
    /// Sayaç kayıtlı olsun olmasın her adres için aynı çalışır. Böylece "kilitlendi" mesajı, bir
    /// adresin sistemde kayıtlı olup olmadığını ele vermez.
    ///
    /// Sayaçlar bellekte durur; uygulama yeniden başlarsa sıfırlanır. Tek sunucuda çalıştığımız için
    /// bu yeterli, veritabanına her hatalı denemede yazmaktan da kurtarır.
    /// </summary>
    public class GirisKilidi
    {
        public const int IzinVerilenHataSayisi = 5;
        public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

        private readonly IMemoryCache _onbellek;
        private readonly object _kilit = new();

        public GirisKilidi(IMemoryCache onbellek)
        {
            _onbellek = onbellek;
        }

        private sealed class Sayac
        {
            public int HataSayisi;
            public DateTime? KilitBitis;
        }

        private static string Anahtar(string normalEposta) => "giris-kilidi:" + normalEposta;

        /// <summary>Adres kilitliyse kalan süreyi, değilse null döner.</summary>
        public TimeSpan? KalanSure(string normalEposta)
        {
            lock (_kilit)
            {
                if (_onbellek.TryGetValue(Anahtar(normalEposta), out Sayac? sayac) && sayac!.KilitBitis is DateTime bitis)
                {
                    var kalan = bitis - DateTime.UtcNow;
                    return kalan > TimeSpan.Zero ? kalan : null;
                }

                return null;
            }
        }

        /// <summary>Hatalı denemeyi sayar. Bu deneme adresi kilitlediyse true döner.</summary>
        public bool HataKaydet(string normalEposta)
        {
            lock (_kilit)
            {
                var anahtar = Anahtar(normalEposta);
                if (!_onbellek.TryGetValue(anahtar, out Sayac? sayac))
                {
                    sayac = new Sayac();
                }

                sayac!.HataSayisi++;

                if (sayac.HataSayisi >= IzinVerilenHataSayisi)
                {
                    // Kilit bitince sayaç da silinir; kişi yeniden 5 deneme hakkıyla başlar.
                    sayac.KilitBitis = DateTime.UtcNow.Add(KilitSuresi);
                    _onbellek.Set(anahtar, sayac, KilitSuresi);
                    return true;
                }

                // Hatalar son denemeden itibaren 15 dakika hatırlanır; arada doğru giriş yapılırsa sıfırlanır.
                _onbellek.Set(anahtar, sayac, KilitSuresi);
                return false;
            }
        }

        /// <summary>Başarılı giriş ya da şifre sıfırlamadan sonra sayacı temizler.</summary>
        public void Sifirla(string normalEposta)
        {
            lock (_kilit)
            {
                _onbellek.Remove(Anahtar(normalEposta));
            }
        }
    }
}
