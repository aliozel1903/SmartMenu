using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Services;
using SmartMenu.ViewModels;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GuvenliResimYukleyici _resimYukleyici;

        public ProfileController(ApplicationDbContext context, GuvenliResimYukleyici resimYukleyici)
        {
            _context = context;
            _resimYukleyici = resimYukleyici;
        }

        private int GetCurrentCafeId()
        {
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
            return int.Parse(cafeIdClaim ?? "0");
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cafe = await _context.Kafeler.FindAsync(GetCurrentCafeId());
            if (cafe == null) return NotFound();

            return View(new ProfileViewModel
            {
                Ad = cafe.Ad,
                Email = cafe.Email,
                LogoUrl = cafe.LogoUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileViewModel model)
        {
            var cafe = await _context.Kafeler.FindAsync(GetCurrentCafeId());
            if (cafe == null) return NotFound();

            if (!ModelState.IsValid)
            {
                // Form hatalıysa sayfa yeniden çizilir; mevcut logo kaybolmasın diye geri yazıyoruz.
                model.LogoUrl = cafe.LogoUrl;
                return View(model);
            }

            cafe.Ad = model.Ad;
            cafe.Email = model.Email;

            // Şifre alanı boş bırakılırsa mevcut hash korunur; doluysa BCrypt ile yeniden hashlenir.
            if (!string.IsNullOrWhiteSpace(model.YeniSifre))
            {
                cafe.SifreHash = BCrypt.Net.BCrypt.HashPassword(model.YeniSifre);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Profil bilgileriniz güncellendi.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Marka logosunu yükler. Logo, QR kodun ortasında ve basılan masa kartında görünür.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoYukle(IFormFile? logo)
        {
            var cafe = await _context.Kafeler.FindAsync(GetCurrentCafeId());
            if (cafe == null) return NotFound();

            if (logo == null || logo.Length == 0)
            {
                TempData["Error"] = "Lütfen bir logo dosyası seçin.";
                return RedirectToAction(nameof(Index));
            }

            // Dosya doğrulaması (boyut, uzantı, içerik tipi ve gerçek format imzası) servis içinde yapılır.
            var (basarili, yol, hata) = await _resimYukleyici.YukleAsync(logo, cafe.Id, cafe.LogoUrl);

            if (!basarili)
            {
                TempData["Error"] = hata;
                return RedirectToAction(nameof(Index));
            }

            // Yeni logo kaydedildikten sonra eskisini diskte bırakmıyoruz.
            var eskiLogo = cafe.LogoUrl;

            cafe.LogoUrl = yol;
            await _context.SaveChangesAsync();

            await _resimYukleyici.SilAsync(eskiLogo);

            TempData["Success"] = "Logonuz güncellendi. QR kodunuzun ortasında artık logonuz görünecek.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoSil()
        {
            var cafe = await _context.Kafeler.FindAsync(GetCurrentCafeId());
            if (cafe == null) return NotFound();

            var eskiLogo = cafe.LogoUrl;

            cafe.LogoUrl = null;
            await _context.SaveChangesAsync();

            await _resimYukleyici.SilAsync(eskiLogo);

            TempData["Success"] = "Logo kaldırıldı. QR kodunda yeniden markanızın baş harfleri kullanılacak.";
            return RedirectToAction(nameof(Index));
        }
    }
}
