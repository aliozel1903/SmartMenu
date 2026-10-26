using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class SiparislerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SiparislerController(ApplicationDbContext context)
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

            // Sadece giriş yapan markanın şubelerine gelen siparişleri listeliyoruz.
            var siparisler = await _context.Siparisler
                .Include(s => s.Sube)
                .Include(s => s.Detaylar)
                    .ThenInclude(d => d.Urun)
                .Where(s => s.Sube != null && s.Sube.CafeId == myCafeId && s.Durum != "Odendi")
                // Aktif siparişler üstte, tamamlananlar en altta kalsın diye sıralamayı durumdan başlatıyoruz.
                .OrderBy(s => s.Durum == "Tamamlandı" ? 1 : s.Durum == "İptal" ? 2 : 0)
                .ThenByDescending(s => s.SiparisTarihi)
                .ToListAsync();

            return View(siparisler);
        }

        // Mutfak fişi: siparişi termal yazıcıya (58/80 mm) uygun dar bir sayfada açar,
        // sayfa açılınca tarayıcının yazdırma penceresi kendiliğinden gelir.
        public async Task<IActionResult> Fis(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var siparis = await _context.Siparisler
                .Include(s => s.Sube)
                    .ThenInclude(s => s!.Cafe)
                .Include(s => s.Detaylar)
                    .ThenInclude(d => d.Urun)
                .FirstOrDefaultAsync(s => s.Id == id && s.Sube != null && s.Sube.CafeId == myCafeId);

            // Başka markanın siparişi de "bulunamadı" görünür; var olup olmadığı belli edilmez.
            if (siparis == null)
            {
                return NotFound();
            }

            return View(siparis);
        }

        public async Task<IActionResult> Bildirimler()
        {
            int myCafeId = GetCurrentCafeId();

            // Bildirimleri şube sahipliği üzerinden filtreliyoruz; başka markanın bildirimi görünmez.
            var bildirimler = await _context.MasaBildirimleri
                .Include(b => b.Sube)
                .Where(b => b.Sube != null && b.Sube.CafeId == myCafeId)
                .OrderBy(b => b.OkunduMu)
                .ThenByDescending(b => b.OlusturulmaTarihi)
                .ToListAsync();

            return View(bildirimler);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BildirimOkundu(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var bildirim = await _context.MasaBildirimleri
                .Include(b => b.Sube)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bildirim == null || bildirim.Sube == null || bildirim.Sube.CafeId != myCafeId)
            {
                TempData["Error"] = "Bildirim bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Bildirimler));
            }

            bildirim.OkunduMu = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bildirim okundu olarak işaretlendi.";
            return RedirectToAction(nameof(Bildirimler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BildirimSil(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var bildirim = await _context.MasaBildirimleri
                .Include(b => b.Sube)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bildirim == null || bildirim.Sube == null || bildirim.Sube.CafeId != myCafeId)
            {
                TempData["Error"] = "Bildirim bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Bildirimler));
            }

            _context.MasaBildirimleri.Remove(bildirim);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bildirim silindi.";
            return RedirectToAction(nameof(Bildirimler));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DurumGuncelle(int id, string durum)
        {
            var izinliDurumlar = new[] { "Bekliyor", "Hazırlanıyor", "Tamamlandı", "İptal" };
            if (!izinliDurumlar.Contains(durum))
            {
                TempData["Error"] = "Geçersiz sipariş durumu.";
                return RedirectToAction(nameof(Index));
            }

            int myCafeId = GetCurrentCafeId();
            var siparis = await _context.Siparisler
                .Include(s => s.Sube)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (siparis == null || siparis.Sube == null || siparis.Sube.CafeId != myCafeId)
            {
                TempData["Error"] = "Sipariş bulunamadı veya bu işlem için yetkiniz yok.";
                return RedirectToAction(nameof(Index));
            }

            siparis.Durum = durum;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Sipariş durumu {durum} olarak güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IptalEt(int id)
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

            // Kayıt geçmişi kaybolmasın diye mutfak ekranındaki silme işlemini İptal durumuna çeviriyoruz.
            siparis.Durum = "İptal";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Sipariş iptal edildi.";
            return RedirectToAction(nameof(Index));
        }
    }
}
