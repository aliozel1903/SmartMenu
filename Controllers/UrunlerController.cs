using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;
using SmartMenu.Services;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class UrunlerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GuvenliResimYukleyici _resimYukleyici;

        public UrunlerController(ApplicationDbContext context, GuvenliResimYukleyici resimYukleyici)
        {
            _context = context;
            _resimYukleyici = resimYukleyici;
        }

        private int GetCurrentCafeId()
        {
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
            return int.Parse(cafeIdClaim ?? "0");
        }

        // Ürünün giriş yapan kafeye ait olup olmadığını tek yerden kontrol ediyoruz.
        private Task<Urun?> KendiUrunumuBulAsync(int id, int cafeId)
        {
            return _context.Urunler
                .Include(u => u.Kategori)
                .FirstOrDefaultAsync(u => u.Id == id && u.Kategori != null && u.Kategori.CafeId == cafeId);
        }

        public async Task<IActionResult> Index()
        {
            int myCafeId = GetCurrentCafeId();
            var urunler = await _context.Urunler
                .Include(u => u.Kategori)
                .Where(u => u.Kategori != null && u.Kategori.CafeId == myCafeId)
                .OrderBy(u => u.Id) // Aktif/Pasif değişince ürün sırası bozulmasın diye eklenme sırasını sabit tutuyoruz.
                .ToListAsync();
            return View(urunler);
        }

        public IActionResult Create()
        {
            int myCafeId = GetCurrentCafeId();
            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad");

            // Yeni ürünler varsayılan olarak menüde görünür gelsin.
            return View(new Urun { AktifMi = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Ad,Icerik,Fiyat,KategoriId,AktifMi")] Urun urun, IFormFile? resim)
        {
            int myCafeId = GetCurrentCafeId();
            var isKategoriValid = await _context.Kategoriler.AnyAsync(k => k.Id == urun.KategoriId && k.CafeId == myCafeId);
            if (!isKategoriValid)
            {
                ModelState.AddModelError("KategoriId", "Geçersiz kategori seçimi.");
            }

            if (ModelState.IsValid && resim != null)
            {
                // Boyut, uzantı ve gerçek dosya imzası kontrolü servis içinde yapılır.
                var (basarili, yol, hata) = await _resimYukleyici.YukleAsync(resim, myCafeId);
                if (basarili)
                {
                    urun.ResimUrl = yol;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, hata ?? "Görsel yüklenemedi.");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(urun);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad", urun.KategoriId);
            return View(urun);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var urun = await KendiUrunumuBulAsync(id, GetCurrentCafeId());

            if (urun != null)
            {
                var eskiResim = urun.ResimUrl;

                _context.Urunler.Remove(urun);
                await _context.SaveChangesAsync();

                // Ürün silinince görseli de temizliyoruz ki çöp birikmesin.
                await _resimYukleyici.SilAsync(eskiResim);

                TempData["Success"] = "Ürün başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "Ürün bulunamadı veya bu işlem için yetkiniz yok.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAktif(int id)
        {
            // Güvenlik için sadece giriş yapan kafenin kendi ürününün aktiflik durumunu değiştirebiliriz.
            var urun = await KendiUrunumuBulAsync(id, GetCurrentCafeId());
            if (urun == null)
            {
                return NotFound();
            }

            urun.AktifMi = !urun.AktifMi;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var urun = await KendiUrunumuBulAsync(id.Value, myCafeId);
            if (urun == null) return NotFound();

            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad", urun.KategoriId);
            return View(urun);
        }

        // resimSilindi: formdaki "Görseli Kaldır" butonundan gelen işaret.
        // ResimUrl kasıtlı olarak formdan bağlanmıyor: mevcut görselin yolu her zaman veritabanından okunur.
        // Aksi halde istemci bu alana başka bir kafenin görselini ya da "../" ile bir sunucu dosyasını yazıp sildirebilirdi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ad,Icerik,Fiyat,KategoriId,AktifMi")] Urun urun, IFormFile? resim, bool resimSilindi = false)
        {
            if (id != urun.Id) return NotFound();

            int myCafeId = GetCurrentCafeId();

            // Düzenlenen ürünün gerçekten bu kafeye ait olduğunu doğruluyoruz.
            // Sadece hedef kategoriyi kontrol etmek yetmez: başka bir kafenin ürün Id'si gönderilip
            // kendi kategorimize taşınarak o ürün ele geçirilebilirdi.
            var mevcutUrun = await KendiUrunumuBulAsync(id, myCafeId);
            if (mevcutUrun == null) return NotFound();

            var isKategoriValid = await _context.Kategoriler.AnyAsync(k => k.Id == urun.KategoriId && k.CafeId == myCafeId);
            if (!isKategoriValid)
            {
                ModelState.AddModelError("KategoriId", "Geçersiz kategori seçimi.");
            }

            string? yeniResimYolu = null;
            if (ModelState.IsValid && resim != null)
            {
                var (basarili, yol, hata) = await _resimYukleyici.YukleAsync(resim, myCafeId, mevcutUrun.ResimUrl);
                if (basarili)
                {
                    yeniResimYolu = yol;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, hata ?? "Görsel yüklenemedi.");
                }
            }

            if (ModelState.IsValid)
            {
                var eskiResim = mevcutUrun.ResimUrl;

                mevcutUrun.Ad = urun.Ad;
                mevcutUrun.Icerik = urun.Icerik;
                mevcutUrun.Fiyat = urun.Fiyat;
                mevcutUrun.KategoriId = urun.KategoriId;
                mevcutUrun.AktifMi = urun.AktifMi;

                if (yeniResimYolu != null)
                {
                    mevcutUrun.ResimUrl = yeniResimYolu;
                }
                else if (resimSilindi)
                {
                    mevcutUrun.ResimUrl = null;
                }

                await _context.SaveChangesAsync();

                // Görsel değiştiyse ya da kaldırıldıysa eskisini temizliyoruz.
                if (eskiResim != mevcutUrun.ResimUrl)
                {
                    await _resimYukleyici.SilAsync(eskiResim);
                }

                return RedirectToAction(nameof(Index));
            }

            // Doğrulama hatasında formun mevcut görseli göstermeye devam etmesi için.
            urun.ResimUrl = mevcutUrun.ResimUrl;
            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad", urun.KategoriId);
            return View(urun);
        }
    }
}
