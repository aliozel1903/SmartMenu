using Microsoft.AspNetCore.Authentication;
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

            // Form hatalıysa sayfa yeniden çizilir; formdan gelmeyen alanları sunucudan geri yazıyoruz.
            model.Email = cafe.Email;
            model.LogoUrl = cafe.LogoUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var sifreDegisti = !string.IsNullOrWhiteSpace(model.YeniSifre);
            if (sifreDegisti &&
                (string.IsNullOrEmpty(model.MevcutSifre) || !BCrypt.Net.BCrypt.Verify(model.MevcutSifre, cafe.SifreHash)))
            {
                ModelState.AddModelError(nameof(model.MevcutSifre), "Şifreni değiştirmek için mevcut şifreni doğru girmelisin.");
                return View(model);
            }

            cafe.Ad = model.Ad;

            // Şifre alanı boş bırakılırsa mevcut hash korunur; doluysa BCrypt ile yeniden hashlenir.
            if (sifreDegisti)
            {
                cafe.SifreHash = BCrypt.Net.BCrypt.HashPassword(model.YeniSifre);
                // Diğer cihazlardaki oturumlar kapanır.
                cafe.GuvenlikDamgasi = OturumYoneticisi.YeniDamga();
            }

            await _context.SaveChangesAsync();

            if (sifreDegisti)
            {
                // Bu cihazdaki oturum yeni damgayla yenilenir; kullanıcı şifresini değiştirdiği yerde çıkış yapmış olmaz.
                var mevcutOturum = await HttpContext.AuthenticateAsync(OturumYoneticisi.Sema);
                await OturumYoneticisi.AcAsync(HttpContext, cafe, mevcutOturum.Properties?.IsPersistent ?? false);
            }

            TempData["Success"] = sifreDegisti
                ? "Profil bilgileriniz ve şifreniz güncellendi. Diğer cihazlardaki oturumlar kapatıldı."
                : "Profil bilgileriniz güncellendi.";

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
