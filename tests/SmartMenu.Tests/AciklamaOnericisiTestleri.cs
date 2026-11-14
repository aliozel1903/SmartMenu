using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmartMenu.Services;

namespace SmartMenu.Tests;

// Ürün açıklaması önerisi (bkz. Services/AciklamaOnericisi.cs). Gerçek Groq'a bağlanılmaz: SahteGroq,
// istenen cevabı döner ve gelen istekleri saklar. Böylece testler ücretsiz kotayı harcamaz, internetsiz de çalışır.
public class AciklamaOnericisiTestleri
{
    private const int KafeId = 1;

    private sealed class SahteGroq : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _cevap;

        public SahteGroq(Func<HttpResponseMessage> cevap) => _cevap = cevap;

        public int IstekSayisi { get; private set; }
        public HttpRequestMessage? SonIstek { get; private set; }
        public string? SonIstekGovdesi { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage istek, CancellationToken iptal)
        {
            IstekSayisi++;
            SonIstek = istek;
            SonIstekGovdesi = istek.Content == null ? null : await istek.Content.ReadAsStringAsync(iptal);
            return _cevap();
        }
    }

    private static HttpResponseMessage Cevap(string icerik, string bitisNedeni = "stop") =>
        Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = icerik }, finish_reason = bitisNedeni } }
        }));

    private static HttpResponseMessage Json(HttpStatusCode durum, string govde) =>
        new(durum) { Content = new StringContent(govde, Encoding.UTF8, "application/json") };

    private static AciklamaOnericisi YeniOnerici(HttpMessageHandler sahte, string? apiAnahtari = "test-anahtari")
    {
        var ayarlar = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Groq:ApiKey"] = apiAnahtari })
            .Build();

        var http = new HttpClient(sahte) { BaseAddress = new Uri("https://api.groq.com/openai/v1/") };

        return new AciklamaOnericisi(http, ayarlar, new MemoryCache(new MemoryCacheOptions()), NullLogger<AciklamaOnericisi>.Instance);
    }

    [Fact]
    public async Task Anahtar_yoksa_ozellik_kapalidir_ve_istek_gitmez()
    {
        var sahte = new SahteGroq(() => Cevap("Bir açıklama."));
        var onerici = YeniOnerici(sahte, apiAnahtari: null);

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.False(onerici.Aktif);
        Assert.Null(sonuc.Aciklama);
        Assert.Contains("kullanılamıyor", sonuc.Hata);
        Assert.Equal(0, sahte.IstekSayisi);
    }

    [Fact]
    public async Task Istek_dogru_adrese_anahtar_ve_urun_bilgisiyle_gider()
    {
        var sahte = new SahteGroq(() => Cevap("Bir açıklama."));
        var onerici = YeniOnerici(sahte);

        await onerici.OnerAsync(KafeId, "San Sebastian", "Tatlılar", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, sahte.SonIstek!.Method);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", sahte.SonIstek.RequestUri!.ToString());
        Assert.Equal("Bearer", sahte.SonIstek.Headers.Authorization!.Scheme);
        Assert.Equal("test-anahtari", sahte.SonIstek.Headers.Authorization.Parameter);

        using var govde = JsonDocument.Parse(sahte.SonIstekGovdesi!);
        var mesajlar = govde.RootElement.GetProperty("messages");
        var kullaniciMesaji = mesajlar[1].GetProperty("content").GetString();
        Assert.Contains("San Sebastian", kullaniciMesaji);
        Assert.Contains("Tatlılar", kullaniciMesaji);
    }

    [Fact]
    public async Task Cevaptaki_tirnaklar_ve_fazla_bosluklar_temizlenir()
    {
        var sahte = new SahteGroq(() => Cevap("  \"Kremamsı dokusu ve\n\n hafif yanık üstüyle  tam bir klasik.\"  "));
        var onerici = YeniOnerici(sahte);

        var sonuc = await onerici.OnerAsync(KafeId, "San Sebastian", null, CancellationToken.None);

        Assert.Null(sonuc.Hata);
        Assert.Equal("Kremamsı dokusu ve hafif yanık üstüyle tam bir klasik.", sonuc.Aciklama);
    }

    [Fact]
    public async Task Uzun_cevap_500_karakteri_gecmeyecek_sekilde_kelime_sinirindan_kesilir()
    {
        var uzunMetin = string.Join(" ", Enumerable.Repeat("lezzetli", 100));
        var sahte = new SahteGroq(() => Cevap(uzunMetin));
        var onerici = YeniOnerici(sahte);

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.NotNull(sonuc.Aciklama);
        Assert.True(sonuc.Aciklama!.Length <= 500);
        Assert.EndsWith("lezzetli", sonuc.Aciklama);
    }

    [Fact]
    public async Task Model_BOS_derse_oneri_uretilemedi_denir()
    {
        var onerici = YeniOnerici(new SahteGroq(() => Cevap("BOS")));

        var sonuc = await onerici.OnerAsync(KafeId, "asdfgh", null, CancellationToken.None);

        Assert.Null(sonuc.Aciklama);
        Assert.Contains("üretilemedi", sonuc.Hata);
    }

    [Fact]
    public async Task Yarim_kalan_cevap_kutuya_konmaz()
    {
        var onerici = YeniOnerici(new SahteGroq(() => Cevap("Taze demlenmiş kahve ve", bitisNedeni: "length")));

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.Null(sonuc.Aciklama);
        Assert.Contains("tamamlanamadı", sonuc.Hata);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "çok fazla istek")]
    [InlineData(HttpStatusCode.Unauthorized, "kullanılamıyor")]
    [InlineData(HttpStatusCode.InternalServerError, "kullanılamıyor")]
    public async Task Groq_hata_donerse_anlasilir_mesaj_verilir(HttpStatusCode durum, string beklenenMesaj)
    {
        var onerici = YeniOnerici(new SahteGroq(() => Json(durum, "{\"error\":{\"message\":\"hata\"}}")));

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.Null(sonuc.Aciklama);
        Assert.Contains(beklenenMesaj, sonuc.Hata);
    }

    [Fact]
    public async Task Beklenmeyen_bicimdeki_cevap_uygulamayi_cokertmez()
    {
        var onerici = YeniOnerici(new SahteGroq(() => Json(HttpStatusCode.OK, "{\"beklenmeyen\":true}")));

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.Null(sonuc.Aciklama);
        Assert.Contains("kullanılamıyor", sonuc.Hata);
    }

    [Fact]
    public async Task Baglanti_hatasinda_tekrar_denenmesi_istenir()
    {
        var onerici = YeniOnerici(new SahteGroq(() => throw new HttpRequestException("bağlantı yok")));

        var sonuc = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.Null(sonuc.Aciklama);
        Assert.Contains("zamanında gelmedi", sonuc.Hata);
    }

    [Fact]
    public async Task Kafe_gunluk_hakkini_doldurunca_istek_gitmez()
    {
        var sahte = new SahteGroq(() => Cevap("Bir açıklama."));
        var onerici = YeniOnerici(sahte);

        for (var i = 0; i < AciklamaOnericisi.KafeBasinaGunlukSinir; i++)
        {
            Assert.NotNull((await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None)).Aciklama);
        }

        var sinirAsildi = await onerici.OnerAsync(KafeId, "Latte", null, CancellationToken.None);

        Assert.Null(sinirAsildi.Aciklama);
        Assert.Contains("hakkınız doldu", sinirAsildi.Hata);
        Assert.Equal(AciklamaOnericisi.KafeBasinaGunlukSinir, sahte.IstekSayisi);

        // Sınır kafe başına: başka bir kafenin hakkı etkilenmez.
        Assert.NotNull((await onerici.OnerAsync(KafeId + 1, "Latte", null, CancellationToken.None)).Aciklama);
    }
}
