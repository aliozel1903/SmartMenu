using System;
using System.Security.Cryptography;

namespace SmartMenu.Services
{
    /// <summary>
    /// QR kodların taşıdığı public token'ı üretir.
    /// Random yerine RandomNumberGenerator kullanılır: Random tahmin edilebilir bir
    /// diziden üretir, bu ise işletim sisteminin kriptografik entropi kaynağını kullanır.
    /// </summary>
    public static class QrTokenUretici
    {
        // 32 bayt = 256 bit entropi. Kaba kuvvetle tahmin edilmesi pratikte imkansız.
        private const int TokenBaytUzunlugu = 32;

        public static string Uret()
        {
            var baytlar = RandomNumberGenerator.GetBytes(TokenBaytUzunlugu);

            // URL'de sorun çıkaran +, / ve = karakterlerini temizleyip
            // adres çubuğuna güvenle yazılabilen bir metin elde ediyoruz.
            return Convert.ToBase64String(baytlar)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
