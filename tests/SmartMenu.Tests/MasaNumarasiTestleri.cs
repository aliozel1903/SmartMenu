using SmartMenu.Services;

namespace SmartMenu.Tests;

// Müşterinin sipariş ve çağrıda yazdığı masa numarası (bkz. Services/MasaNumarasi.cs).
public class MasaNumarasiTestleri
{
    [Theory]
    [InlineData("1", "1")]
    [InlineData("12", "12")]
    [InlineData("9999", "9999")]
    [InlineData(" 12 ", "12")]
    // Baştaki sıfırlar atılır: "07" ile "7" aynı masa sayılır.
    [InlineData("07", "7")]
    [InlineData("0012", "12")]
    public void Gecerli_numara_sayiya_cevrilir(string girilen, string beklenen)
    {
        Assert.Equal(beklenen, MasaNumarasi.Normalize(girilen));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("0000")]
    [InlineData("A5")]
    [InlineData("Bahçe 3")]
    [InlineData("1 2")]
    [InlineData("-3")]
    [InlineData("+3")]
    [InlineData("3.5")]
    [InlineData("12345")]
    // Arapça "٣" gibi başka alfabelerin rakamları da kabul edilmez; sadece 0-9.
    [InlineData("٣")]
    public void Gecersiz_numara_reddedilir(string? girilen)
    {
        Assert.Null(MasaNumarasi.Normalize(girilen));
    }
}
