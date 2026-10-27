using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;
using SmartMenu.Services;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class SubelerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubelerController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentCafeId()
        {
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
            return int.Parse(cafeIdClaim ?? "0");
        }

        public async Task<IActionResult> Index()
        {
            int myCafeId = GetCurrentCafeId();

            // Merkez/marka hesabı sadece kendi şubelerini görür.
            var subeler = await _context.Subeler
                .Where(s => s.CafeId == myCafeId)
                .OrderBy(s => s.Id)
                .ToListAsync();

            return View(subeler);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSiparisAktif(int id)
        {
            int myCafeId = GetCurrentCafeId();

            // Tek tıkla sipariş kilidini değiştirirken de şubenin giriş yapan markaya ait olduğunu kontrol ediyoruz.
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.Id == id && s.CafeId == myCafeId);
            if (sube == null)
            {
                TempData["Error"] = "Şube bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            sube.IsSiparisAktif = !sube.IsSiparisAktif;
            await _context.SaveChangesAsync();

            TempData["Success"] = sube.IsSiparisAktif
                ? "Bu şube için online siparişler açıldı."
                : "Bu şube için online siparişler kapatıldı.";

            return RedirectToAction(nameof(Index));
        }

        public IActionResult Create()
        {
            return View(new Sube());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SubeAdi,Telefon,Adres")] Sube sube)
        {
            if (ModelState.IsValid)
            {
                sube.CafeId = GetCurrentCafeId();
                sube.OlusturulmaTarihi = DateTime.UtcNow;

                // Şubenin QR kimliği formdan gelmez; her zaman sunucuda kriptografik olarak üretilir.
                sube.QrToken = QrTokenUretici.Uret();
                sube.QrTokenTarihi = DateTime.UtcNow;

                _context.Subeler.Add(sube);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(sube);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.Id == id && s.CafeId == myCafeId);
            if (sube == null) return NotFound();

            return View(sube);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,SubeAdi,Telefon,Adres,GarsonCagriAktif,HesapIsteAktif")] Sube sube)
        {
            if (id != sube.Id) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var existingSube = await _context.Subeler.FirstOrDefaultAsync(s => s.Id == id && s.CafeId == myCafeId);
            if (existingSube == null) return NotFound();

            if (ModelState.IsValid)
            {
                // CafeId ve oluşturulma tarihi formdan gelmez; güvenli tarafta mevcut kayıttan korunur.
                existingSube.SubeAdi = sube.SubeAdi;
                existingSube.Telefon = sube.Telefon;
                existingSube.Adres = sube.Adres;
                existingSube.GarsonCagriAktif = sube.GarsonCagriAktif;
                existingSube.HesapIsteAktif = sube.HesapIsteAktif;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(sube);
        }

        /// <summary>
        /// Şubenin QR kodunu geçersiz kılıp yenisini üretir.
        /// WhatsApp'taki "diğer oturumları kapat" mantığının karşılığı: basılı QR çalınır,
        /// fotoğraflanıp paylaşılır veya kötüye kullanılırsa eski link anında ölür.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QrYenile(int id)
        {
            int myCafeId = GetCurrentCafeId();

            // Başka bir markanın şubesinin QR'ını yenileyemesin diye burada da tenant kontrolü yapıyoruz.
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.Id == id && s.CafeId == myCafeId);
            if (sube == null)
            {
                TempData["Error"] = "Şube bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            sube.QrToken = QrTokenUretici.Uret();
            sube.QrTokenTarihi = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{sube.SubeAdi} şubesinin QR kodu yenilendi. Eski QR kod artık çalışmıyor, lütfen yeni kodu masalara yerleştirin.";
            return RedirectToAction("QrCode", "Menu", new { subeId = sube.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.Id == id && s.CafeId == myCafeId);

            if (sube != null && await _context.Siparisler.AnyAsync(s => s.SubeId == sube.Id))
            {
                // Şube silinseydi siparişleri de silinir, Kasa'daki ciro ve satış geçmişi kaybolurdu.
                TempData["Error"] = $"{sube.SubeAdi} şubesinin geçmiş siparişleri olduğu için silinemez. Sipariş almayı durdurmak için \"Sipariş Açık\" düğmesiyle kapatabilirsiniz.";
            }
            else if (sube != null)
            {
                _context.Subeler.Remove(sube);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Şube başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "Şube bulunamadı veya bu işlem için yetkiniz yok.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
