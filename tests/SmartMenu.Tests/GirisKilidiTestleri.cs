using Microsoft.Extensions.Caching.Memory;
using SmartMenu.Services;

namespace SmartMenu.Tests;

// Aynı adrese üst üste hatalı şifre girilince girişin kapanması (bkz. Services/GirisKilidi.cs).
public class GirisKilidiTestleri
{
    private const string Eposta = "kafe@example.test";

    private static GirisKilidi YeniKilit() => new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void Hic_hata_yoksa_adres_kilitli_degildir()
    {
        var kilit = YeniKilit();

        Assert.Null(kilit.KalanSure(Eposta));
    }

    [Fact]
    public void Izin_verilen_sayidan_az_hata_kilitlemez()
    {
        var kilit = YeniKilit();

        for (var i = 1; i < GirisKilidi.IzinVerilenHataSayisi; i++)
        {
            Assert.False(kilit.HataKaydet(Eposta));
        }

        Assert.Null(kilit.KalanSure(Eposta));
    }

    [Fact]
    public void Besinci_hata_adresi_kilit_suresi_kadar_kilitler()
    {
        var kilit = YeniKilit();

        for (var i = 1; i < GirisKilidi.IzinVerilenHataSayisi; i++)
        {
            kilit.HataKaydet(Eposta);
        }

        Assert.True(kilit.HataKaydet(Eposta));

        var kalan = kilit.KalanSure(Eposta);
        Assert.NotNull(kalan);
        Assert.InRange(kalan.Value, GirisKilidi.KilitSuresi - TimeSpan.FromSeconds(5), GirisKilidi.KilitSuresi);
    }

    [Fact]
    public void Basarili_giris_sayaci_sifirlar()
    {
        var kilit = YeniKilit();

        for (var i = 1; i < GirisKilidi.IzinVerilenHataSayisi; i++)
        {
            kilit.HataKaydet(Eposta);
        }

        kilit.Sifirla(Eposta);

        // Sıfırlandıktan sonra tek bir hata kilitlememeli; kişi yeniden tam hakla başlar.
        Assert.False(kilit.HataKaydet(Eposta));
        Assert.Null(kilit.KalanSure(Eposta));
    }

    [Fact]
    public void Sifirla_kilitli_adresi_de_acar()
    {
        var kilit = YeniKilit();

        for (var i = 0; i < GirisKilidi.IzinVerilenHataSayisi; i++)
        {
            kilit.HataKaydet(Eposta);
        }

        kilit.Sifirla(Eposta);

        Assert.Null(kilit.KalanSure(Eposta));
    }

    [Fact]
    public void Bir_adresin_hatalari_baska_adresi_etkilemez()
    {
        var kilit = YeniKilit();

        for (var i = 0; i < GirisKilidi.IzinVerilenHataSayisi; i++)
        {
            kilit.HataKaydet(Eposta);
        }

        Assert.NotNull(kilit.KalanSure(Eposta));
        Assert.Null(kilit.KalanSure("baska@example.test"));
    }
}
