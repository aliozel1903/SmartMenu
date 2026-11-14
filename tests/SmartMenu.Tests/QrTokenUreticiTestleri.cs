using System.Text.RegularExpressions;
using SmartMenu.Services;

namespace SmartMenu.Tests;

// Masadaki QR kodun taşıdığı token (bkz. Services/QrTokenUretici.cs). Menü adresinde durduğu için
// URL'de sorun çıkarmamalı ve tahmin edilemeyecek kadar uzun olmalı.
public class QrTokenUreticiTestleri
{
    [Fact]
    public void Token_sadece_adres_cubugunda_guvenli_karakterlerden_olusur()
    {
        var token = QrTokenUretici.Uret();

        // Base64'ün "+", "/" ve "=" karakterleri adreste bozulur; yerlerine "-" ve "_" kullanılmalı.
        Assert.Matches(new Regex("^[A-Za-z0-9_-]+$"), token);
    }

    [Fact]
    public void Token_256_bit_tasir()
    {
        var token = QrTokenUretici.Uret();

        // 32 bayt, sonundaki "=" atılmış base64'te 43 karakter eder.
        Assert.Equal(43, token.Length);

        var standartBase64 = token.Replace('-', '+').Replace('_', '/') + "=";
        Assert.Equal(32, Convert.FromBase64String(standartBase64).Length);
    }

    [Fact]
    public void Her_cagri_farkli_token_uretir()
    {
        var tokenlar = Enumerable.Range(0, 1000).Select(_ => QrTokenUretici.Uret()).ToHashSet();

        Assert.Equal(1000, tokenlar.Count);
    }
}
