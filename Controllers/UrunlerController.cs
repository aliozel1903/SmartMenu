using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;
using SmartMenu.Services;
using SmartMenu.ViewModels;
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
        private readonly AciklamaOnericisi _aciklamaOnericisi;

        public UrunlerController(ApplicationDbContext context, GuvenliResimYukleyici resimYukleyici, AciklamaOnericisi aciklamaOnericisi)
        {
            _context = context;
            _resimYukleyici = resimYukleyici;
            _aciklamaOnericisi = aciklamaOnericisi;
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

        // Formdaki "Yanına İyi Gider" alanı için kafenin diğer ürünlerini hazırlar.
        private async Task OneriSecimiHazirlaAsync(int cafeId, int? urunId, IEnumerable<int> secili)
        {
            var adaylar = await _context.Urunler
                .Include(u => u.Kategori)
                .Where(u => u.Kategori != null && u.Kategori.CafeId == cafeId && (urunId == null || u.Id != urunId))
                .OrderBy(u => u.Kategori!.Sira)
                .ThenBy(u => u.KategoriId)
                .ThenBy(u => u.Id)
                .ToListAsync();

            ViewBag.OneriSecimi = new OneriSecimiViewModel { Adaylar = adaylar, Secili = secili.ToHashSet() };
        }

        // Formdan gelen öneri listesini süzer. Tarayıcıdan gelen veriye güvenmiyoruz: sınır aşılırsa,
        // ürün kendisini önerirse ya da başka bir kafenin ürünü gönderilirse hata verir.
        private async Task<List<int>> OnerileriDogrulaAsync(IEnumerable<int>? oneriler, int cafeId, int? urunId)
        {
            var istenen = (oneriler ?? Enumerable.Empty<int>()).Distinct().ToList();

            if (istenen.Count > UrunEslesme.EnFazlaOneri)
            {
                ModelState.AddModelError(string.Empty, $"En fazla {UrunEslesme.EnFazlaOneri} öneri seçebilirsiniz.");
                return istenen;
            }

            var gecerliSayisi = await _context.Urunler
                .CountAsync(u => istenen.Contains(u.Id) && u.Id != urunId && u.Kategori != null && u.Kategori.CafeId == cafeId);
            if (gecerliSayisi != istenen.Count)
            {
                ModelState.AddModelError(string.Empty, "Geçersiz öneri seçimi.");
            }

            return istenen;
        }

        // Formdaki "Açıklama Öner" düğmesi. Yapay zekânın önerisini döner, hiçbir şey kaydetmez:
        // metin kutuya yazılır, kafe sahibi okuyup düzeltir ve formu kendisi kaydeder.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AciklamaOner(string? ad, int? kategoriId, CancellationToken iptal)
        {
            var urunAdi = (ad ?? string.Empty).Trim();
            if (urunAdi.Length == 0 || urunAdi.Length > 100)
            {
                return Json(new { hata = "Önce ürün adını yazın (en fazla 100 karakter)." });
            }

            int myCafeId = GetCurrentCafeId();

            // Kategori adı sadece bu kafenin kendi kategorisiyse kullanılır.
            var kategoriAdi = kategoriId == null ? null : await _context.Kategoriler
                .Where(k => k.Id == kategoriId && k.CafeId == myCafeId)
                .Select(k => k.Ad)
                .FirstOrDefaultAsync(iptal);

            var sonuc = await _aciklamaOnericisi.OnerAsync(myCafeId, urunAdi, kategoriAdi, iptal);
            return Json(new { aciklama = sonuc.Aciklama, hata = sonuc.Hata });
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

        public async Task<IActionResult> Create()
        {
            int myCafeId = GetCurrentCafeId();
            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad");
            await OneriSecimiHazirlaAsync(myCafeId, null, Enumerable.Empty<int>());

            // Yeni ürünler varsayılan olarak menüde görünür gelsin.
            return View(new Urun { AktifMi = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Ad,Icerik,Fiyat,KategoriId,AktifMi")] Urun urun, IFormFile? resim, List<int>? oneriler)
        {
            int myCafeId = GetCurrentCafeId();
            var isKategoriValid = await _context.Kategoriler.AnyAsync(k => k.Id == urun.KategoriId && k.CafeId == myCafeId);
            if (!isKategoriValid)
            {
                ModelState.AddModelError("KategoriId", "Geçersiz kategori seçimi.");
            }

            var oneriIdleri = await OnerileriDogrulaAsync(oneriler, myCafeId, null);

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
                // Ürünün Id'si kayıtta oluşur; EF eşleşmelere onu kendisi yazar.
                foreach (var oneriId in oneriIdleri)
                {
                    urun.Eslesmeler.Add(new UrunEslesme { OnerilenUrunId = oneriId });
                }

                _context.Add(urun);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.KategoriId = new SelectList(_context.Kategoriler.Where(k => k.AktifMi && k.CafeId == myCafeId), "Id", "Ad", urun.KategoriId);
            await OneriSecimiHazirlaAsync(myCafeId, null, oneriIdleri);
            return View(urun);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var urun = await KendiUrunumuBulAsync(id, GetCurrentCafeId());

            if (urun != null && await _context.SiparisDetaylari.AnyAsync(d => d.UrunId == urun.Id))
            {
                // Geçmiş siparişler ürüne bağlı; silinirse Kasa'daki ciro ve sipariş geçmişi bozulurdu
                // (veritabanı da buna izin vermiyor, eskiden burada hata sayfası çıkıyordu). Ürünü gizliyoruz.
                urun.AktifMi = false;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{urun.Ad} geçmiş siparişlerde yer aldığı için silinmedi, menüden gizlendi.";
            }
            else if (urun != null)
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
            var seciliOneriler = await _context.UrunEslesmeleri
                .Where(e => e.UrunId == urun.Id)
                .Select(e => e.OnerilenUrunId)
                .ToListAsync();
            await OneriSecimiHazirlaAsync(myCafeId, urun.Id, seciliOneriler);
            return View(urun);
        }

        // resimSilindi: formdaki "Görseli Kaldır" butonundan gelen işaret.
        // ResimUrl kasıtlı olarak formdan bağlanmıyor: mevcut görselin yolu her zaman veritabanından okunur.
        // Aksi halde istemci bu alana başka bir kafenin görselini ya da "../" ile bir sunucu dosyasını yazıp sildirebilirdi.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Ad,Icerik,Fiyat,KategoriId,AktifMi")] Urun urun, IFormFile? resim, List<int>? oneriler, bool resimSilindi = false)
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

            var oneriIdleri = await OnerileriDogrulaAsync(oneriler, myCafeId, id);

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

                // Önerilerde sadece farkı uyguluyoruz: kaldırılanları sil, yeni seçilenleri ekle.
                var mevcutEslesmeler = await _context.UrunEslesmeleri.Where(e => e.UrunId == id).ToListAsync();
                _context.UrunEslesmeleri.RemoveRange(mevcutEslesmeler.Where(e => !oneriIdleri.Contains(e.OnerilenUrunId)));
                foreach (var oneriId in oneriIdleri.Where(o => mevcutEslesmeler.All(e => e.OnerilenUrunId != o)))
                {
                    _context.UrunEslesmeleri.Add(new UrunEslesme { UrunId = id, OnerilenUrunId = oneriId });
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
            await OneriSecimiHazirlaAsync(myCafeId, id, oneriIdleri);
            return View(urun);
        }
    }
}
