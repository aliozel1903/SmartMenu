using System.Globalization;
using System.Linq;

namespace SmartMenu.Services
{
    /// <summary>
    /// Müşterinin yazdığı masa numarası kuralları (sipariş ve garson/hesap çağrısı).
    ///
    /// Masa numarası sadece rakam olabilir. Menüdeki kutu harfi zaten engelliyor, ama istek elle de
    /// gönderilebildiği için sunucu da kontrol eder. "07" ile "7" aynı masa sayılsın diye sayıya çevrilir;
    /// böylece mutfak ekranında ve masa başına sınırlarda tek bir masa numarası görünür.
    /// </summary>
    public static class MasaNumarasi
    {
        public const int EnBuyuk = 9999;

        public const string Hata = "Masa numarası 1 ile 9999 arasında bir sayı olmalı.";

        /// <summary>Geçerliyse sayıya çevrilmiş halini ("07" → "7"), değilse null döner.</summary>
        public static string? Normalize(string? masaNo)
        {
            var metin = masaNo?.Trim();
            if (string.IsNullOrEmpty(metin) || metin.Length > 4 || !metin.All(char.IsAsciiDigit))
            {
                return null;
            }

            var sayi = int.Parse(metin, CultureInfo.InvariantCulture);
            return sayi is >= 1 and <= EnBuyuk ? sayi.ToString(CultureInfo.InvariantCulture) : null;
        }
    }
}
