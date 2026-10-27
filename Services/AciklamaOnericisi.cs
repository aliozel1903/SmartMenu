using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SmartMenu.Services
{
    public record AciklamaSonucu(string? Aciklama, string? Hata);

    /// <summary>
    /// Ürün adından kısa, iştah açıcı bir menü açıklaması önerir (Anthropic Claude API).
    ///
    /// Öneri sadece formdaki kutuya yazılır; kafe sahibi okuyup düzeltmeden hiçbir şey kaydedilmez.
    ///
    /// Her çağrı ücretli olduğu için günlük sınırlar var: kafe başına ve tüm site için. Sayaçlar bellekte
    /// tutulur (uygulama yeniden başlarsa sıfırlanır); asıl kesin sınır Anthropic hesabındaki ön ödemeli
    /// bakiyedir, bakiye bitince öneri "şu an kullanılamıyor" der.
    ///
    /// API anahtarı yoksa (ör. yerelde) özellik kapalıdır ve formda düğme görünmez.
    /// </summary>
    public class AciklamaOnericisi
    {
        private const string Model = "claude-opus-5-5";
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

        private readonly AnthropicClient? _istemci;
        private readonly IMemoryCache _onbellek;
        private readonly ILogger<AciklamaOnericisi> _logger;
        private readonly object _kilit = new();

        public AciklamaOnericisi(IConfiguration configuration, IMemoryCache onbellek, ILogger<AciklamaOnericisi> logger)
        {
            _onbellek = onbellek;
            _logger = logger;

            var anahtar = configuration["Anthropic:ApiKey"];
            if (!string.IsNullOrWhiteSpace(anahtar))
            {
                // Kafe sahibi ekranda bekliyor; SDK'nın varsayılan 10 dakikalık süresi yerine kısa tutuyoruz.
                _istemci = new AnthropicClient { ApiKey = anahtar, Timeout = TimeSpan.FromSeconds(30) };
            }
        }

        public bool Aktif => _istemci != null;

        public async Task<AciklamaSonucu> OnerAsync(int cafeId, string urunAdi, string? kategoriAdi, CancellationToken iptal)
        {
            if (_istemci == null)
            {
                return new AciklamaSonucu(null, "Açıklama önerisi şu an kullanılamıyor.");
            }

            if (!HakKullan(cafeId))
            {
                return new AciklamaSonucu(null, $"Bugünlük öneri hakkınız doldu (günde {KafeBasinaGunlukSinir}). Yarın tekrar deneyebilirsiniz.");
            }

            var istek = $"Ürün adı: {urunAdi}";
            if (!string.IsNullOrWhiteSpace(kategoriAdi))
            {
                istek += $"\nKategori: {kategoriAdi}";
            }

            // SDK hata alınca birkaç kez yeniden dener; kafe sahibi toplamda en fazla 30 saniye bekler.
            using var sure = CancellationTokenSource.CreateLinkedTokenSource(iptal);
            sure.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                var yanit = await _istemci.Beta.Messages.Create(new MessageCreateParams
                {
                    Model = Model,
                    MaxTokens = 2000,
                    // Basit bir yazma işi; düşük efor hem hızlı hem ucuz.
                    OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
                    // Model isteği güvenlik gerekçesiyle reddederse Anthropic aynı isteği uygun bir modelle tekrar dener.
                    Betas = ["server-side-fallback-2026-07-01"],
                    Fallbacks = new Default(),
                    System = SistemMetni,
                    Messages = [new() { Role = Role.User, Content = istek }],
                }, sure.Token);

                if (yanit.StopReason == BetaStopReason.Refusal)
                {
                    return new AciklamaSonucu(null, "Bu ürün adı için öneri üretilemedi.");
                }

                var metin = string.Concat(yanit.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
                var aciklama = Temizle(metin);

                if (aciklama.Length == 0 || aciklama.Equals(UretilemediIsareti, StringComparison.OrdinalIgnoreCase))
                {
                    return new AciklamaSonucu(null, "Bu ürün adı için öneri üretilemedi. Ürün adını kontrol edin.");
                }

                return new AciklamaSonucu(aciklama, null);
            }
            catch (AnthropicRateLimitException ex)
            {
                _logger.LogWarning(ex, "Açıklama önerisi: Anthropic hız sınırı");
                return new AciklamaSonucu(null, "Şu an çok fazla istek var. Biraz sonra tekrar deneyin.");
            }
            catch (AnthropicApiException ex)
            {
                // Bakiye bitmesi, geçersiz anahtar gibi durumlar. Anahtar loglara yazılmaz, sadece hata.
                _logger.LogError(ex, "Açıklama önerisi: Anthropic API hatası");
                return new AciklamaSonucu(null, "Açıklama önerisi şu an kullanılamıyor.");
            }
            catch (Exception ex) when (ex is AnthropicIOException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Açıklama önerisi: bağlantı hatası ya da zaman aşımı");
                return new AciklamaSonucu(null, "Öneri zamanında gelmedi. Tekrar deneyin.");
            }
        }

        // Günlük hakkı düşer. Kafe ya da tüm site sınırı dolmuşsa false döner.
        private bool HakKullan(int cafeId)
        {
            var gun = DateTime.Today.ToString("yyyy-MM-dd");
            var kafeAnahtari = $"aciklama:{gun}:kafe:{cafeId}";
            var toplamAnahtari = $"aciklama:{gun}:toplam";

            lock (_kilit)
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
