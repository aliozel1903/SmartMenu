using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartMenu.Services
{
    /// <summary>
    /// Hesap e-postalarını (doğrulama, şifre sıfırlama) Resend API'si üzerinden gönderir.
    ///
    /// API anahtarı depoda değil, canlı ortamda "Resend__ApiKey" ortam değişkeninde durur.
    /// Geliştirmede anahtar yoksa e-posta gönderilmez; bağlantı konsola yazılır, böylece akış yerelde de denenebilir.
    ///
    /// E-postalara kullanıcının yazdığı hiçbir metin (kafe adı vb.) konmaz: kayıt formu herkese açık olduğu için
    /// biri başkasının adresiyle kayıt olup kafe adına bir dolandırıcılık mesajı yazabilir ve bizim alan adımızdan
    /// o kişiye ulaştırabilirdi.
    /// </summary>
    public class EpostaGonderici
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _env;
        private readonly ILogger<EpostaGonderici> _logger;

        public EpostaGonderici(HttpClient http, IConfiguration configuration, IHostEnvironment env, ILogger<EpostaGonderici> logger)
        {
            _http = http;
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        public Task<bool> DogrulamaBaglantisiGonderAsync(string alici, string baglanti) =>
            GonderAsync(
                alici,
                "SmartMenu hesabını doğrula",
                "E-posta adresini doğrula",
                "SmartMenu hesabını kullanmaya başlamak için e-posta adresini doğrulaman gerekiyor.",
                "Hesabımı Doğrula",
                baglanti,
                "Bu bağlantı 24 saat geçerlidir. SmartMenu'ya bu adresle kayıt olmadıysan bu e-postayı yok sayabilirsin.");

        public Task<bool> SifreSifirlamaBaglantisiGonderAsync(string alici, string baglanti) =>
            GonderAsync(
                alici,
                "SmartMenu şifre sıfırlama",
                "Şifreni sıfırla",
                "SmartMenu hesabın için şifre sıfırlama isteği aldık. Yeni şifreni belirlemek için aşağıdaki butona tıkla.",
                "Yeni Şifre Belirle",
                baglanti,
                "Bu bağlantı 1 saat geçerlidir ve yalnızca bir kez kullanılabilir. Bu isteği sen yapmadıysan bu e-postayı yok sayabilirsin; şifren değişmez.");

        private async Task<bool> GonderAsync(string alici, string konu, string baslik, string aciklama, string butonMetni, string baglanti, string dipnot)
        {
            var metin = $"{baslik}\n\n{aciklama}\n\n{baglanti}\n\n{dipnot}";

            var apiAnahtari = _configuration["Resend:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiAnahtari))
            {
                if (_env.IsDevelopment())
                {
                    _logger.LogWarning("Resend:ApiKey tanımlı değil, e-posta gönderilmedi. Alıcı: {Alici}\n{Metin}", alici, metin);
                    return true;
                }

                _logger.LogError("Resend:ApiKey tanımlı değil, e-posta gönderilemedi.");
                return false;
            }

            using var istek = new HttpRequestMessage(HttpMethod.Post, "emails");
            istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiAnahtari);
            istek.Content = JsonContent.Create(new
            {
                from = _configuration["Eposta:Gonderen"],
                to = new[] { alici },
                subject = konu,
                html = HtmlOlustur(baslik, aciklama, butonMetni, baglanti, dipnot),
                text = metin
            });

            try
            {
                using var yanit = await _http.SendAsync(istek);
                if (yanit.IsSuccessStatusCode)
                {
                    return true;
                }

                // Resend'in hata mesajı sorunun nedenini söyler (ör. doğrulanmamış alan adı). Alıcı adresi
                // kişisel veri olduğu için canlı ortam kayıtlarına yazılmaz.
                _logger.LogError("Resend e-postayı kabul etmedi ({Durum}): {Yanit}",
                    (int)yanit.StatusCode, await yanit.Content.ReadAsStringAsync());
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "Resend'e bağlanılamadı.");
            }

            return false;
        }

        // E-posta istemcileri (Gmail, Outlook) harici CSS'i desteklemediği için stiller satır içinde.
        private static string HtmlOlustur(string baslik, string aciklama, string butonMetni, string baglanti, string dipnot) => $"""
            <!DOCTYPE html>
            <html lang="tr">
            <body style="margin:0;padding:24px;background:#f8f9fa;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:16px;padding:32px;">
                    <tr><td>
                      <p style="margin:0 0 24px;font-size:20px;font-weight:bold;color:#2563eb;">Smart<span style="color:#1f2937;">Menu</span></p>
                      <h1 style="margin:0 0 12px;font-size:20px;">{baslik}</h1>
                      <p style="margin:0 0 24px;font-size:15px;line-height:22px;color:#4b5563;">{aciklama}</p>
                      <a href="{baglanti}" style="display:inline-block;padding:14px 24px;background:#2563eb;color:#ffffff;text-decoration:none;font-weight:bold;border-radius:12px;">{butonMetni}</a>
                      <p style="margin:24px 0 0;font-size:13px;line-height:20px;color:#6b7280;">Buton çalışmazsa bu bağlantıyı tarayıcına yapıştır:<br><span style="word-break:break-all;">{baglanti}</span></p>
                      <p style="margin:24px 0 0;font-size:13px;line-height:20px;color:#6b7280;">{dipnot}</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
