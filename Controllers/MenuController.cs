using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Models;
using SmartMenu.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace SmartMenu.Controllers
{
    // DİKKAT: Buraya [Authorize] KOYMUYORUZ çünkü müşteriler şifresiz girmeli!
    // Sınıf seviyesinde genel yetkilendirme yok; müşteri menüsü açık, admin QR paneli ise action seviyesinde korumalı.
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // URL'den artık sıralı subeId değil, tahmin edilemez QR token gelir.
        // Böylece kimse adres çubuğunda ?subeId=1,2,3 deneyerek başka işletmelerin menüsünü gezemez (IDOR koruması).
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? token = null)
        {
            Sube? sube = null;

            if (!string.IsNullOrWhiteSpace(token))
            {
                sube = await _context.Subeler
                    .Include(s => s.Cafe)
                    .FirstOrDefaultAsync(s => s.QrToken == token);
            }
            else if (User.Identity?.IsAuthenticated == true)
            {
                // Token yoksa bu, admin panelindeki "Menüyü Görüntüle" önizlemesidir.
                // Anonim ziyaretçi bu dala hiç giremez; giriş yapan yönetici de yalnızca kendi markasının şubesini görür.
                var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
                if (int.TryParse(cafeIdClaim, out var claimCafeId))
                {
                    sube = await _context.Subeler
                        .Include(s => s.Cafe)
                        .Where(s => s.CafeId == claimCafeId)
                        .OrderBy(s => s.Id)
                        .FirstOrDefaultAsync();
                }
            }

            // Geçersiz/iptal edilmiş token ile gelinirse "şube yok" ile aynı cevabı veriyoruz.
            // Token'ın var olup olmadığını sızdıran farklı bir mesaj vermiyoruz.
            if (sube == null || sube.Cafe == null)
            {
                return NotFound();
            }

            var cafeId = sube.CafeId;
            var kafe = sube.Cafe;

            // Kafe yoksa boş menü göstermek yerine 404 döndürmek daha doğru.
            if (kafe == null)
            {
                return NotFound();
            }

            // Müşteri menüyü her açtığında bugünün ziyaret sayacını artırıyoruz.
            // Aynı gün için kayıt varsa sayıyı artırır, yoksa yeni günlük kayıt oluşturur.
            // Sayacı şube kırılımında tutuyoruz: hangi şubenin QR'ının okutulduğunu bilmezsek,
            // taramalar düştüğünde hangi şubenin masalarına bakılacağını da söyleyemeyiz.
            var bugun = DateTime.Today;
            var bugununZiyareti = await _context.MenuZiyaretleri
                .FirstOrDefaultAsync(z => z.CafeId == cafeId && z.SubeId == sube.Id && z.Tarih == bugun);

            if (bugununZiyareti == null)
            {
                _context.MenuZiyaretleri.Add(new SmartMenu.Models.MenuZiyaret
                {
                    CafeId = cafeId,
                    SubeId = sube.Id,
                    Tarih = bugun,
                    ZiyaretSayisi = 1
                });
            }
            else
            {
                bugununZiyareti.ZiyaretSayisi++;
            }

            await _context.SaveChangesAsync();

            // Veritabanından sadece bu kafeye ait olan,
            // "AktifMi" durumu true olan kategorileri ve içindeki ürünleri Include ile getir.
            var kategoriler = await _context.Kategoriler
                .Include(k => k.Urunler!.Where(u => u.AktifMi)) // İçindeki sadece aktif ürünleri al
                    .ThenInclude(u => u.Eslesmeler) // "Yanına iyi gider" önerilerinin Id'leri
                .Include(k => k.Cafe) // Kafenin temel bilgisini de ilişki üzerinden hazır tut
                .Where(k => k.CafeId == cafeId && k.AktifMi == true) // İŞTE FİLTRE BURADA!
                .OrderBy(k => k.Sira)
                .ToListAsync();

            // ViewBag yerine tek ve temiz bir ViewModel gönderiyoruz.
            // Böylece View tarafında Model.Cafe ve Model.Kategoriler şeklinde okunabilir bir yapı oluşuyor.
            var viewModel = new MenuViewModel
            {
                Cafe = kafe,
                Sube = sube,
                Kategoriler = kategoriler
            };

            return View(viewModel);
        }

        [AllowAnonymous]
        [HttpPost]
        // Giriş gerektirmeyen bu uca aynı IP'den gelen istek sayısı sınırlanır (sipariş/bildirim spam'i).
        [EnableRateLimiting("herkeseAcik")]
        public async Task<IActionResult> BildirimGonder([FromBody] MasaBildirimViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.MasaNo) || string.IsNullOrWhiteSpace(model.BildirimTipi))
            {
                return BadRequest(new { success = false, message = "Bildirim bilgileri eksik." });
            }

            var izinliBildirimTipleri = new[] { "Garson", "Hesap" };
            if (!izinliBildirimTipleri.Contains(model.BildirimTipi))
            {
                return BadRequest(new { success = false, message = "Geçersiz bildirim tipi." });
            }

            // Şubeyi istemcinin gönderdiği Id'ye göre değil, QR token'ına göre buluyoruz.
            // İstemci hangi şubeye bildirim gideceğini artık seçemez.
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.QrToken == model.Token);
            if (sube == null)
            {
                return NotFound(new { success = false, message = "Geçersiz QR kod. Lütfen masanızdaki kodu yeniden okutun." });
            }

            if (model.BildirimTipi == "Garson" && !sube.GarsonCagriAktif)
            {
                return BadRequest(new { success = false, message = "Bu şubede garson çağırma özelliği şu anda kapalı." });
            }

            if (model.BildirimTipi == "Hesap" && !sube.HesapIsteAktif)
            {
                return BadRequest(new { success = false, message = "Bu şubede hesap isteme özelliği şu anda kapalı." });
            }

            var masaNo = model.MasaNo.Trim();
            var ucDakikaOnce = DateTime.UtcNow.AddMinutes(-3);

            // Spam koruması: aynı şube ve masadan son 3 dakikada okunmamış çağrı varsa yeni kayıt açmıyoruz.
            var mevcutOkunmamisCagriVar = await _context.MasaBildirimleri
                .AnyAsync(b => b.SubeId == sube.Id
                    && b.MasaNo == masaNo
                    && !b.OkunduMu
                    && b.OlusturulmaTarihi >= ucDakikaOnce);

            if (mevcutOkunmamisCagriVar)
            {
                return Ok(new
                {
                    success = false,
                    message = "Mevcut bir çağrınız zaten var. Lütfen personelimizin gelmesini bekleyin."
                });
            }

            _context.MasaBildirimleri.Add(new MasaBildirim
            {
                SubeId = sube.Id,
                MasaNo = masaNo,
                BildirimTipi = model.BildirimTipi,
                OlusturulmaTarihi = DateTime.UtcNow,
                OkunduMu = false
            });

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Çağrınız personele iletildi." });
        }

        [AllowAnonymous]
        [HttpPost]
        // Giriş gerektirmeyen bu uca aynı IP'den gelen istek sayısı sınırlanır (sipariş/bildirim spam'i).
        [EnableRateLimiting("herkeseAcik")]
        public async Task<IActionResult> SiparisOlustur([FromBody] SiparisOlusturViewModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Token) || string.IsNullOrWhiteSpace(model.MasaNo) || model.Urunler == null || !model.Urunler.Any())
            {
                return BadRequest(new { message = "Sipariş bilgileri eksik." });
            }

            // Siparişin hangi şubeye düşeceğini istemci belirlemiyor; QR token'ından sunucu çözüyor.
            var sube = await _context.Subeler.FirstOrDefaultAsync(s => s.QrToken == model.Token);
            if (sube == null)
            {
                return NotFound(new { message = "Geçersiz QR kod. Lütfen masanızdaki kodu yeniden okutun." });
            }

            // İşletme sipariş kilidini kapattıysa menü görünür kalır ama sipariş oluşturulamaz.
            if (!sube.IsSiparisAktif)
            {
                return BadRequest(new { message = "Bu şube şu anda online sipariş almıyor." });
            }

            var urunIdleri = model.Urunler.Select(u => u.UrunId).Distinct().ToList();

            // Fiyatı istemciden almıyoruz. Ürünlerin gerçekten bu markaya ait ve aktif olduğunu veritabanından kontrol ediyoruz.
            var urunler = await _context.Urunler
                .Include(u => u.Kategori)
                .Where(u => urunIdleri.Contains(u.Id)
                    && u.AktifMi
                    && u.Kategori != null
                    && u.Kategori.CafeId == sube.CafeId)
                .ToListAsync();

            if (urunler.Count != urunIdleri.Count)
            {
                return BadRequest(new { message = "Sepette geçersiz veya pasif ürün var." });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var siparis = new Siparis
            {
                SubeId = sube.Id,
                MasaNo = model.MasaNo.Trim(),
                SiparisTarihi = DateTime.UtcNow,
                Durum = "Bekliyor"
            };

            foreach (var kalem in model.Urunler.Where(u => u.Adet > 0))
            {
                var urun = urunler.First(u => u.Id == kalem.UrunId);

                siparis.Detaylar.Add(new SiparisDetay
                {
                    UrunId = urun.Id,
                    Adet = kalem.Adet,
                    BirimFiyat = urun.Fiyat
                });

                siparis.ToplamTutar += urun.Fiyat * kalem.Adet;
            }

            if (!siparis.Detaylar.Any())
            {
                return BadRequest(new { message = "Sepette ürün bulunmuyor." });
            }

            _context.Siparisler.Add(siparis);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Siparişiniz mutfağa iletildi.", siparisId = siparis.Id });
        }

        [Authorize]
        public IActionResult QrCode(int subeId = 0)
        {
            // Giriş yapan yöneticinin CafeId bilgisini Login sırasında oluşturulan Claim'den okuyoruz.
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;

            if (!int.TryParse(cafeIdClaim, out var cafeId))
            {
                return Unauthorized();
            }

            var subeler = _context.Subeler
                .Where(s => s.CafeId == cafeId)
                .OrderBy(s => s.Id)
                .ToList();

            // QR yenilendikten sonra kullanıcıyı doğru şube seçili halde geri getirebilmek için.
            ViewBag.SeciliSubeId = subeler.Any(s => s.Id == subeId) ? subeId : subeler.FirstOrDefault()?.Id ?? 0;

            // Masa kartında ve QR'ın ortasındaki rozette markanın adı ve logosu görünecek.
            var marka = _context.Kafeler
                .Where(k => k.Id == cafeId)
                .Select(k => new { k.Ad, k.LogoUrl })
                .FirstOrDefault();

            ViewBag.KafeAdi = marka?.Ad ?? "SmartMenu";
            ViewBag.KafeLogoUrl = marka?.LogoUrl;

            return View(subeler);
        }
    }
}
