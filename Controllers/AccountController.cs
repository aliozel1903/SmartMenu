using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;
using System.Collections.Generic;
using SmartMenu.Data;
using SmartMenu.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using SmartMenu.Services;

namespace SmartMenu.Controllers
{
    public class AccountController : Controller
    {
        private const int MinimumSifreUzunlugu = 8;

        private readonly ApplicationDbContext _context;
        private readonly HesapTokenServisi _tokenServisi;
        private readonly EpostaGonderici _epostaGonderici;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public AccountController(ApplicationDbContext context, HesapTokenServisi tokenServisi,
            EpostaGonderici epostaGonderici, IConfiguration configuration, IWebHostEnvironment env)
        {
            _context = context;
            _tokenServisi = tokenServisi;
            _epostaGonderici = epostaGonderici;
            _configuration = configuration;
            _env = env;
        }

        // 1. GİRİŞ SAYFASINI AÇ
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 2. FORMU DOLDURUP GÖNDERDİĞİNDE
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Kaba kuvvet şifre denemesi ve toplu sahte hesap açmayı yavaşlatır.
        [EnableRateLimiting("giris")]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            var cafe = await KafeyiBulAsync(email);

            if (cafe != null && BCrypt.Net.BCrypt.Verify(password, cafe.SifreHash))
            {
                // Bu uyarı sadece şifre doğruysa gösterilir; böylece başkası bir adresin kayıtlı olup
                // olmadığını bu mesaj üzerinden öğrenemez.
                if (!cafe.EmailDogrulandi)
                {
                    ViewBag.DogrulanmamisEposta = cafe.Email;
                    ModelState.AddModelError(string.Empty, "E-posta adresin henüz doğrulanmadı. Gelen kutundaki doğrulama bağlantısına tıkla.");
                    return View();
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, cafe.Ad ?? email),
                    new Claim(ClaimTypes.Email, cafe.Email ?? email),
                    new Claim("CafeId", cafe.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, "AdminCookies");
                var principal = new ClaimsPrincipal(identity);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
                };

                await HttpContext.SignInAsync("AdminCookies", principal, authProperties);

                return RedirectToAction("Index", "Dashboard");
            }

            ModelState.AddModelError(string.Empty, "Geçersiz e-posta veya şifre");
            return View();
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // Kaba kuvvet şifre denemesi ve toplu sahte hesap açmayı yavaşlatır.
        [EnableRateLimiting("giris")]
        public async Task<IActionResult> Register(string cafeName, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(cafeName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Hata = "Lütfen tüm alanları doldurunuz.";
                return View();
            }

            email = EpostaNormalize(email);

            if (!new EmailAddressAttribute().IsValid(email))
            {
                ViewBag.Hata = "Geçerli bir e-posta adresi giriniz.";
                return View();
            }

            // Veritabanı sütun uzunlukları; aşılırsa kayıt sırasında hata sayfası çıkardı.
            if (cafeName.Trim().Length > 100 || email.Length > 150)
            {
                ViewBag.Hata = "Kafe adı en fazla 100, e-posta adresi en fazla 150 karakter olabilir.";
                return View();
            }

            var sifreHatasi = SifreHatasi(password, password);
            if (sifreHatasi != null)
            {
                ViewBag.Hata = sifreHatasi;
                return View();
            }

            var existingCafeExists = await KafeyiBulAsync(email) != null;
            if (existingCafeExists)
            {
                ViewBag.Hata = "Bu e-posta adresi zaten kullanımda.";
                return View();
            }

            var newCafe = new Cafe
            {
                Ad = cafeName.Trim(),
                Email = email,
                SifreHash = BCrypt.Net.BCrypt.HashPassword(password),
                EmailDogrulandi = false
            };

            _context.Kafeler.Add(newCafe);
            await _context.SaveChangesAsync();

            // Franchise yapısında her yeni marka için başlangıçta bir merkez şube oluşturuyoruz.
            _context.Subeler.Add(new Sube
            {
                CafeId = newCafe.Id,
                SubeAdi = newCafe.Ad,
                OlusturulmaTarihi = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await DogrulamaBaglantisiGonderAsync(newCafe);

            TempData["DogrulamaEposta"] = email;
            return RedirectToAction(nameof(Dogrulama));
        }

        // E-POSTA DOĞRULAMA
        // Kayıttan sonra açılan "gelen kutunu kontrol et" sayfası. Bağlantı gelmediyse buradan yenisi istenir.
        [HttpGet]
        public IActionResult Dogrulama()
        {
            ViewBag.Eposta = TempData["DogrulamaEposta"] as string;
            ViewBag.Hata = TempData["Hata"] as string;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("eposta")]
        public async Task<IActionResult> Dogrulama(string email)
        {
            var cafe = await KafeyiBulAsync(email);
            if (cafe != null && !cafe.EmailDogrulandi)
            {
                await DogrulamaBaglantisiGonderAsync(cafe);
            }

            // Hesap olsa da olmasa da aynı cevap: bu form bir adresin kayıtlı olup olmadığını sorgulamak için kullanılamaz.
            ViewBag.Bilgi = "Bu adresle doğrulanmamış bir hesap varsa yeni bir doğrulama bağlantısı gönderdik. Gelen kutunu ve spam klasörünü kontrol et.";
            return View();
        }

        // E-postadaki doğrulama bağlantısı buraya gelir.
        [HttpGet]
        public async Task<IActionResult> EpostaDogrula(string? token)
        {
            var cafe = await _tokenServisi.KullanAsync(token, EpostaTokenAmaci.EpostaDogrulama);
            if (cafe == null)
            {
                TempData["Hata"] = "Bu doğrulama bağlantısı geçersiz, süresi dolmuş ya da daha önce kullanılmış. " +
                                   "Hesabını zaten doğruladıysan giriş yapabilirsin; doğrulamadıysan aşağıdan yeni bağlantı iste.";
                return RedirectToAction(nameof(Dogrulama));
            }

            cafe.EmailDogrulandi = true;
            await _context.SaveChangesAsync();

            TempData["Basari"] = "E-posta adresin doğrulandı. Artık giriş yapabilirsin.";
            return RedirectToAction(nameof(Login));
        }

        // ŞİFREMİ UNUTTUM
        [HttpGet]
        public IActionResult SifremiUnuttum()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("eposta")]
        public async Task<IActionResult> SifremiUnuttum(string email)
        {
            var cafe = await KafeyiBulAsync(email);
            if (cafe != null)
            {
                var token = await _tokenServisi.UretAsync(cafe.Id, EpostaTokenAmaci.SifreSifirlama);
                if (token != null)
                {
                    await _epostaGonderici.SifreSifirlamaBaglantisiGonderAsync(cafe.Email!, BaglantiOlustur(nameof(SifreSifirla), token));
                }
            }

            ViewBag.Bilgi = "Bu adresle kayıtlı bir hesap varsa şifre sıfırlama bağlantısı gönderdik. Gelen kutunu ve spam klasörünü kontrol et.";
            return View();
        }

        // E-postadaki şifre sıfırlama bağlantısı buraya gelir. Bağlantıyı açmak token'ı harcamaz; bazı e-posta
        // servisleri güvenlik taraması için bağlantıları önceden açtığından, token sadece form gönderilince kullanılır.
        [HttpGet]
        public async Task<IActionResult> SifreSifirla(string? token)
        {
            ViewBag.Token = token;
            ViewBag.Gecersiz = await _tokenServisi.GecerliTokeninKafesiAsync(token, EpostaTokenAmaci.SifreSifirlama) == null;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("giris")]
        public async Task<IActionResult> SifreSifirla(string? token, string sifre, string sifreTekrar)
        {
            ViewBag.Token = token;

            var sifreHatasi = SifreHatasi(sifre, sifreTekrar);
            if (sifreHatasi != null)
            {
                ViewBag.Hata = sifreHatasi;
                return View();
            }

            var cafe = await _tokenServisi.KullanAsync(token, EpostaTokenAmaci.SifreSifirlama);
            if (cafe == null)
            {
                ViewBag.Gecersiz = true;
                return View();
            }

            cafe.SifreHash = BCrypt.Net.BCrypt.HashPassword(sifre);
            // Bağlantıya tıklayabildiğine göre e-posta adresi gerçekten bu kişiye ait.
            cafe.EmailDogrulandi = true;
            await _context.SaveChangesAsync();

            TempData["Basari"] = "Şifren güncellendi. Yeni şifrenle giriş yapabilirsin.";
            return RedirectToAction(nameof(Login));
        }

        // 3. ÇIKIŞ YAP (GÜVENLİ ÇIKIŞ)
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminCookies");
            return RedirectToAction("Index", "Home"); 
        }

        // E-posta adresleri büyük/küçük harf duyarsız karşılaştırılır: "Ali@x.com" ile kayıt olan
        // "ali@x.com" yazarak da giriş yapabilmeli ve aynı adresle ikinci bir hesap açılamamalı.
        private static string EpostaNormalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        private Task<Cafe?> KafeyiBulAsync(string? email)
        {
            var normal = EpostaNormalize(email);
            return _context.Kafeler.FirstOrDefaultAsync(k => k.Email!.ToLower() == normal);
        }

        private static string? SifreHatasi(string? sifre, string? sifreTekrar)
        {
            if (string.IsNullOrEmpty(sifre) || sifre.Length < MinimumSifreUzunlugu)
            {
                return $"Şifre en az {MinimumSifreUzunlugu} karakter olmalı.";
            }

            return sifre == sifreTekrar ? null : "Şifreler birbiriyle aynı değil.";
        }

        private async Task DogrulamaBaglantisiGonderAsync(Cafe cafe)
        {
            var token = await _tokenServisi.UretAsync(cafe.Id, EpostaTokenAmaci.EpostaDogrulama);
            if (token != null)
            {
                await _epostaGonderici.DogrulamaBaglantisiGonderAsync(cafe.Email!, BaglantiOlustur(nameof(EpostaDogrula), token));
            }
        }

        // E-postadaki bağlantının alan adı isteğin Host başlığından değil, ayarlardan alınır. Aksi halde saldırgan
        // "şifremi unuttum" isteğini sahte bir Host başlığıyla gönderip kurbana kendi sitesine giden, içinde geçerli
        // token olan bir bağlantı attırabilirdi (password reset poisoning).
        private string BaglantiOlustur(string action, string token)
        {
            var siteAdresi = _configuration["Site:Adres"];
            if (string.IsNullOrWhiteSpace(siteAdresi))
            {
                if (!_env.IsDevelopment())
                {
                    throw new InvalidOperationException("Site:Adres ayarı tanımlı değil; e-posta bağlantısı oluşturulamıyor.");
                }

                siteAdresi = $"{Request.Scheme}://{Request.Host}";
            }

            return siteAdresi.TrimEnd('/') + Url.Action(action, "Account", new { token });
        }
    }
}
