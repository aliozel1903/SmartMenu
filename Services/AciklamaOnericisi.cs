using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SmartMenu.Services
{
    public record AciklamaSonucu(string? Aciklama, string? Hata);

    /// <summary>
    /// Ürün adından kısa, iştah açıcı bir menü açıklaması önerir. Groq'un ücretsiz API katmanını kullanır
    /// (kart gerektirmez; Groq girilen metni model eğitiminde kullanmaz).
    ///
    /// Öneri sadece formdaki kutuya yazılır; kafe sahibi okuyup düzeltmeden hiçbir şey kaydedilmez.
    ///
    /// Ücretsiz katmanın günlük istek ve token kotası var. Kotayı tek bir kafe tüketmesin diye kafe başına ve
    /// tüm site için günlük sınır koyuyoruz. Sayaçlar bellekte tutulur, uygulama yeniden başlarsa sıfırlanır;
    /// kota yine de aşılırsa Groq 429 döner ve kullanıcıya "biraz sonra deneyin" denir, ücret çıkmaz.
    ///
    /// API anahtarı canlıda "Groq__ApiKey" ortam değişkenindedir. Anahtar yoksa özellik kapalıdır ve
    /// formda düğme görünmez.
    /// </summary>
    public class AciklamaOnericisi
    {
        // Groq'un ücretsiz katmanında günde 1000 istek / 200 bin token veren, Türkçesi iyi bir model.
        private const string Model = "openai/gpt-oss-120b";
        public const int KafeBasinaGunlukSinir = 20;
        private const int ToplamGunlukSinir = 150;

        // Ürün.Icerik alanı en fazla 500 karakter.
        private const int EnFazlaUzunluk = 500;

        // Ürün adı yiyecek/içecek değilse model bu kelimeyi döner.
        private const string UretilemediIsareti = "BOS";

        private const string SistemMetni =
            "Bir kafenin dijital menüsü için ürün açıklaması yazıyorsun. Sana bir ürün adı ve kategorisi verilecek. " +
            "Türkçe, sıcak ve iştah açıcı, kısa bir menü açıklaması yaz.\n\n" +
            "Kurallar:\n" +
            "- En fazla 2 cümle ve 250 karakter.\n" +
            "- Sadece açıklama metnini yaz: başlık, tırnak işareti, emoji ya da madde işareti kullanma.\n" +
            "- Ürün adından anlaşılamayacak iddialarda bulunma. İçerik listesi, alerjen, kalori, sağlık faydası, " +
            "menşe ya da \"ev yapımı\", \"organik\", \"glutensiz\" gibi doğrulanamayan bilgiler yazma; bunları kafe sahibi " +
            "bilir, yanlış bilgi müşteriyi yanıltır.\n" +
            "- Fiyat, indirim ya da kampanya yazma.\n" +
            "- Ürün adı bir yiyecek ya da içecek değilse veya anlamsızsa sadece " + UretilemediIsareti + " yaz.";

        // HttpClient'lı servisler her istekte yeniden oluşur; sayaç kilidi tüm örnekler için ortak olmalı.
        private static readonly object Kilit = new();

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _onbellek;
        private readonly ILogger<AciklamaOnericisi> _logger;

        public AciklamaOnericisi(HttpClient http, IConfiguration configuration, IMemoryCache onbellek, ILogger<AciklamaOnericisi> logger)
        {
            _http = http;
            _configuration = configuration;
            _onbellek = onbellek;
            _logger = logger;
        }

        private string? ApiAnahtari => _configuration["Groq:ApiKey"];

        public bool Aktif => !string.IsNullOrWhiteSpace(ApiAnahtari);

        public async Task<AciklamaSonucu> OnerAsync(int cafeId, string urunAdi, string? kategoriAdi, CancellationToken iptal)
        {
            if (!Aktif)
            {
                return new AciklamaSonucu(null, "Açıklama önerisi şu an kullanılamıyor.");
            }

            if (!HakKullan(cafeId))
            {
                return new AciklamaSonucu(null, $"Bugünlük öneri hakkınız doldu (günde {KafeBasinaGunlukSinir}). Yarın tekrar deneyebilirsiniz.");
            }

            var istekMetni = $"Ürün adı: {urunAdi}";
            if (!string.IsNullOrWhiteSpace(kategoriAdi))
            {
                istekMetni += $"\nKategori: {kategoriAdi}";
            }

            using var istek = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiAnahtari);
            istek.Content = JsonContent.Create(new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = SistemMetni },
                    new { role = "user", content = istekMetni },
                },
                // Basit bir yazma işi: düşünme payını kısa tutup modelin düşüncesini cevaba eklemiyoruz.
                reasoning_effort = "low",
                include_reasoning = false,
                max_completion_tokens = 1000,
                temperature = 0.7,
            });

            try
            {
                using var yanit = await _http.SendAsync(istek, iptal);

                if (yanit.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Açıklama önerisi: Groq ücretsiz kotası doldu (429)");
                    return new AciklamaSonucu(null, "Şu an çok fazla istek var. Biraz sonra tekrar deneyin.");
                }

                if (!yanit.IsSuccessStatusCode)
                {
                    // Gövde loglanır ama anahtar istek başlığında olduğu için loga girmez.
                    _logger.LogError("Açıklama önerisi: Groq hatası {Durum}: {Govde}", (int)yanit.StatusCode, await yanit.Content.ReadAsStringAsync(iptal));
                    return new AciklamaSonucu(null, "Açıklama önerisi şu an kullanılamıyor.");
                }

                using var belge = await JsonDocument.ParseAsync(await yanit.Content.ReadAsStreamAsync(iptal), cancellationToken: iptal);
                var secim = belge.RootElement.GetProperty("choices")[0];

                // "length": metin token sınırında yarım kaldı; yarım cümleyi kutuya koymuyoruz.
                if (secim.TryGetProperty("finish_reason", out var bitis) && bitis.GetString() == "length")
                {
                    return new AciklamaSonucu(null, "Öneri tamamlanamadı. Tekrar deneyin.");
                }

                var aciklama = Temizle(secim.GetProperty("message").GetProperty("content").GetString() ?? string.Empty);

                if (aciklama.Length == 0 || aciklama.Equals(UretilemediIsareti, StringComparison.OrdinalIgnoreCase))
                {
                    return new AciklamaSonucu(null, "Bu ürün adı için öneri üretilemedi. Ürün adını kontrol edin.");
                }

                return new AciklamaSonucu(aciklama, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Zaman aşımı (30 sn, bkz. Program.cs) ya da bağlantı hatası.
                _logger.LogWarning(ex, "Açıklama önerisi: bağlantı hatası ya da zaman aşımı");
                return new AciklamaSonucu(null, "Öneri zamanında gelmedi. Tekrar deneyin.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogError(ex, "Açıklama önerisi: Groq cevabı beklenen biçimde değil");
                return new AciklamaSonucu(null, "Açıklama önerisi şu an kullanılamıyor.");
            }
        }

        // Günlük hakkı düşer. Kafe ya da tüm site sınırı dolmuşsa false döner.
        private bool HakKullan(int cafeId)
        {
            var gun = DateTime.Today.ToString("yyyy-MM-dd");
            var kafeAnahtari = $"aciklama:{gun}:kafe:{cafeId}";
            var toplamAnahtari = $"aciklama:{gun}:toplam";

            lock (Kilit)
            {
                var kafeSayisi = _onbellek.Get<int>(kafeAnahtari);
                var toplamSayi = _onbellek.Get<int>(toplamAnahtari);

                if (kafeSayisi >= KafeBasinaGunlukSinir || toplamSayi >= ToplamGunlukSinir)
                {
                    return false;
                }

                _onbellek.Set(kafeAnahtari, kafeSayisi + 1, TimeSpan.FromDays(1));
                _onbellek.Set(toplamAnahtari, toplamSayi + 1, TimeSpan.FromDays(1));
                return true;
            }
        }

        // Model kurallara rağmen tırnak ya da fazladan boşluk ekleyebilir; kutuya temiz metin gitsin.
        private static string Temizle(string metin)
        {
            var temiz = Regex.Replace(metin, @"\s+", " ").Trim().Trim('"', '“', '”', '\'');

            if (temiz.Length > EnFazlaUzunluk)
            {
                var kesim = temiz.LastIndexOf(' ', EnFazlaUzunluk);
                temiz = temiz[..(kesim > 0 ? kesim : EnFazlaUzunluk)].TrimEnd();
            }

            return temiz;
        }
    }
}
