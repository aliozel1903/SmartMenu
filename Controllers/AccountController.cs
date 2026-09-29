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

namespace SmartMenu.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
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
            var cafe = await _context.Kafeler.FirstOrDefaultAsync(k => k.Email == email);

            if (cafe != null && BCrypt.Net.BCrypt.Verify(password, cafe.SifreHash))
            {
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

            var existingCafeExists = await _context.Kafeler.AnyAsync(k => k.Email == email);
            if (existingCafeExists)
            {
                ViewBag.Hata = "Bu e-posta adresi zaten kullanımda.";
                return View();
            }

            var newCafe = new Cafe
            {
                Ad = cafeName,
                Email = email,
                SifreHash = BCrypt.Net.BCrypt.HashPassword(password)
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

            return RedirectToAction("Login");
        }

        // 3. ÇIKIŞ YAP (GÜVENLİ ÇIKIŞ)
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminCookies");
            return RedirectToAction("Index", "Home"); 
        }
    }
}
