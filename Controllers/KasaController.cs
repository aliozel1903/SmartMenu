using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class KasaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KasaController(ApplicationDbContext context)
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

            // Kasa ekranı sadece tamamlanmış ve ödenmiş siparişleri gösterir; aktif mutfak siparişleri burada görünmez.
            var siparisler = await _context.Siparisler
                .Include(s => s.Sube)
                .Include(s => s.Detaylar)
                    .ThenInclude(d => d.Urun)
                .Where(s => s.Sube != null
                    && s.Sube.CafeId == myCafeId
                    && (s.Durum == "Tamamlandı" || s.Durum == "Odendi"))
                .OrderBy(s => s.Durum == "Odendi" ? 1 : 0)
                .ThenByDescending(s => s.SiparisTarihi)
                .ToListAsync();

            return View(siparisler);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OdendiYap(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var siparis = await _context.Siparisler
                .Include(s => s.Sube)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (siparis == null || siparis.Sube == null || siparis.Sube.CafeId != myCafeId)
            {
                TempData["Error"] = "Sipariş bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            if (siparis.Durum != "Tamamlandı")
            {
                TempData["Error"] = "Sadece tamamlanan siparişler ödenmiş olarak işaretlenebilir.";
                return RedirectToAction(nameof(Index));
            }

            siparis.Durum = "Odendi";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Sipariş ödendi olarak işaretlendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sil(int id, string sifre)
        {
            int myCafeId = GetCurrentCafeId();
            var siparis = await _context.Siparisler
                .Include(s => s.Sube)
                .Include(s => s.Detaylar)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (siparis == null || siparis.Sube == null || siparis.Sube.CafeId != myCafeId)
            {
                TempData["Error"] = "Sipariş bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            var cafe = await _context.Kafeler.FirstOrDefaultAsync(c => c.Id == myCafeId);
            if (cafe == null || string.IsNullOrWhiteSpace(sifre) || string.IsNullOrWhiteSpace(cafe.SifreHash) || !BCrypt.Net.BCrypt.Verify(sifre, cafe.SifreHash))
            {
                TempData["Error"] = "Yönetici şifresi hatalı.";
                return RedirectToAction(nameof(Index));
            }

            // Kasa ekranındaki güvenli silme gerçek silme yapar; detay kayıtları cascade ile birlikte temizlenir.
            _context.Siparisler.Remove(siparis);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Sipariş kalıcı olarak silindi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
