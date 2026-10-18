using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMenu.Data;
using SmartMenu.Models;

namespace SmartMenu.Services
{
    /// <summary>
    /// Yönetim paneli oturumlarını açar ve her istekte geçerliliğini kontrol eder.
    ///
    /// Her hesabın rastgele bir güvenlik damgası var ve giriş çerezi bu damgayı taşıyor. Şifre değişince damga
    /// yenilenir; eski damgayı taşıyan çerezler (ör. şifreyi çalıp giriş yapmış birinin tarayıcısı) bir sonraki
    /// istekte reddedilir. Çerez tek başına imzalı bir bilet olduğu için, bu kontrol olmadan şifre değiştirmek
    /// zaten açık olan oturumları kapatmazdı.
    /// </summary>
    public static class OturumYoneticisi
    {
        public const string Sema = "AdminCookies";
        private const string DamgaClaim = "GuvenlikDamgasi";

        public static Task AcAsync(HttpContext http, Cafe cafe, bool beniHatirla)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, cafe.Ad ?? cafe.Email ?? string.Empty),
                new Claim(ClaimTypes.Email, cafe.Email ?? string.Empty),
                new Claim("CafeId", cafe.Id.ToString()),
                new Claim(DamgaClaim, cafe.GuvenlikDamgasi)
            };

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Sema));

            return http.SignInAsync(Sema, principal, new AuthenticationProperties
            {
                IsPersistent = beniHatirla,
                ExpiresUtc = beniHatirla ? DateTimeOffset.UtcNow.AddDays(30) : null
            });
        }

        // Çerez doğrulama olayı: çerezdeki damga veritabanındakiyle aynı değilse (ya da hesap silinmişse) oturum kapatılır.
        public static async Task DogrulaAsync(CookieValidatePrincipalContext context)
        {
            var cafeIdMetni = context.Principal?.FindFirst("CafeId")?.Value;
            var damga = context.Principal?.FindFirst(DamgaClaim)?.Value;

            var gecerli = false;
            if (int.TryParse(cafeIdMetni, out var cafeId) && !string.IsNullOrEmpty(damga))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                gecerli = await db.Kafeler.AnyAsync(k => k.Id == cafeId && k.GuvenlikDamgasi == damga);
            }

            if (!gecerli)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(Sema);
            }
        }

        public static string YeniDamga() => QrTokenUretici.Uret();
    }
}
