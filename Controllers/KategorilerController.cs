using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Eklendi: Dropdown (Açılır Liste) menüleri için gerekli
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SmartMenu.Controllers
{
    [Authorize]
    public class KategorilerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public KategorilerController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int GetCurrentCafeId()
        {
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
            return int.Parse(cafeIdClaim ?? "0");
        }

        // GET: Kategoriler (Kategorileri Listeleme Ekranı)
        public async Task<IActionResult> Index()
        {
            // 1. Giriş yapan adamın bilekliğindeki (Claim) CafeId'yi oku
            int myCafeId = GetCurrentCafeId();

            // 2. LINQ İzolasyonu: Sadece CafeId'si bizim adamınkiyle eşleşenleri getir!
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Cafe)
                .Where(k => k.CafeId == myCafeId) // İŞTE GÜVENLİK DUVARI BURASI
                .OrderBy(k => k.Sira) // Hazır gelmişken sıraya göre dizelim
                .ToListAsync();

            return View(kategoriler);
        }

        // ==============================================================
        // YENİ EKLENEN "CREATE" (KATEGORİ EKLEME) METOTLARI AŞAĞIDADIR
        // ==============================================================

        // GET: Kategoriler/Create (Sayfayı Ekrana Getiren Metot)
        public IActionResult Create()
        {
            return View();
        }

        // POST: Kategoriler/Create (Kaydet Butonuna Basılınca Çalışan Metot)
        [HttpPost]
        [ValidateAntiForgeryToken]
        // DİKKAT: Bind içinden CafeId'yi sildik, çünkü formdan almayacağız, biz basacağız!
        public async Task<IActionResult> Create([Bind("Id,Ad,Sira,AktifMi")] Kategori kategori)
        {
            if (ModelState.IsValid)
            {
                // 1. Adamın kim olduğunu bul
                int myCafeId = GetCurrentCafeId();

                // 2. Yeni kategoriye adamın Kafe ID'sini mühürle!
                kategori.CafeId = myCafeId;

                // 3. Veritabanına kaydet
                _context.Add(kategori);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(kategori);
        }
        // ==============================================================
        // DÜZENLEME (EDIT) İŞLEMLERİ
        // ==============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.Id == id && k.CafeId == myCafeId);
            if (kategori == null) return NotFound();

            return View(kategori);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ad,Sira,AktifMi")] Kategori kategori)
        {
            if (id != kategori.Id) return NotFound();

            int myCafeId = GetCurrentCafeId();
            kategori.CafeId = myCafeId; // Tempering önlemi

            if (ModelState.IsValid)
            {
                _context.Update(kategori);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(kategori);
        }

        // ==============================================================
        // SİLME (DELETE) İŞLEMLERİ
        // ==============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var kategori = await _context.Kategoriler
                .Include(k => k.Cafe)
                .FirstOrDefaultAsync(m => m.Id == id && m.CafeId == myCafeId);

            if (kategori == null) return NotFound();

            return View(kategori);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            int myCafeId = GetCurrentCafeId();
            var kategori = await _context.Kategoriler.FirstOrDefaultAsync(k => k.Id == id && k.CafeId == myCafeId);
            if (kategori != null)
            {
                _context.Kategoriler.Remove(kategori);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Kategori başarıyla silindi.";
            }
            else
            {
                TempData["Error"] = "Kategori bulunamadı veya bu işlem için yetkiniz yok.";
            }
            return RedirectToAction(nameof(Index));
        }
        // ==============================================================
        // DETAY (DETAILS) İŞLEMLERİ
        // ==============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            int myCafeId = GetCurrentCafeId();
            var kategori = await _context.Kategoriler
                .Include(k => k.Cafe)
                .FirstOrDefaultAsync(m => m.Id == id && m.CafeId == myCafeId);

            if (kategori == null) return NotFound();

            return View(kategori);
        }
    }

}
