using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Services;
using SmartMenu.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace SmartMenu.Controllers
{
    [Authorize] // Güvenlik görevlimiz: Giriş yapmayan patron paneline giremez
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly TaramaAnomaliServisi _taramaAnomaliServisi;

        // Veritabanı bağlantımızı kurucu metotla içeri alıyoruz
        public DashboardController(ApplicationDbContext context, TaramaAnomaliServisi taramaAnomaliServisi)
        {
            _context = context;
            _taramaAnomaliServisi = taramaAnomaliServisi;
        }

        public async Task<IActionResult> Index()
        {
            var cafeIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CafeId")?.Value;
            if (!int.TryParse(cafeIdClaim, out var cafeId))
            {
                return Unauthorized();
            }

            // 1. Sadece giriş yapan kafenin ürünlerini sayıyoruz.
            var toplamUrun = await _context.Urunler
                .Include(u => u.Kategori)
                .CountAsync(u => u.Kategori != null && u.Kategori.CafeId == cafeId);

            // 2. Sadece giriş yapan kafenin kategorilerini sayıyoruz.
            var toplamKategori = await _context.Kategoriler
                .CountAsync(k => k.CafeId == cafeId);

            var bugun = DateTime.Today;
            var haftaninIlkGunu = bugun.AddDays(-6);

            // 3. Son 7 günün ziyaret kayıtlarını tek seferde çekiyoruz.
            var ziyaretler = await _context.MenuZiyaretleri
                .Where(z => z.CafeId == cafeId && z.Tarih >= haftaninIlkGunu && z.Tarih <= bugun)
                .ToListAsync();

            var trCulture = new CultureInfo("tr-TR");
            var etiketler = new List<string>();
            var veriler = new List<int>();

            // Chart.js için eksik günleri 0 ile dolduruyoruz; böylece grafik her zaman 7 kolon/puan gösterir.
            // Ziyaretler artık şube kırılımında tutuluyor; bir günde birden fazla satır olabilir.
            // Grafik marka geneli gösterdiği için o günün tüm şubelerini topluyoruz.
            for (var tarih = haftaninIlkGunu; tarih <= bugun; tarih = tarih.AddDays(1))
            {
                etiketler.Add(trCulture.DateTimeFormat.GetAbbreviatedDayName(tarih.DayOfWeek));
                veriler.Add(ziyaretler.Where(z => z.Tarih == tarih).Sum(z => z.ZiyaretSayisi));
            }

            var viewModel = new DashboardViewModel
            {
                ToplamUrun = toplamUrun,
                ToplamKategori = toplamKategori,
                BugunZiyaret = ziyaretler.Where(z => z.Tarih == bugun).Sum(z => z.ZiyaretSayisi),
                HaftalikZiyaretEtiketleri = etiketler,
                HaftalikZiyaretVerileri = veriler,

                // QR kodu kapatılmış/değiştirilmiş olabilecek şubeleri tespit ediyoruz.
                QrTaramaUyarilari = await _taramaAnomaliServisi.AnomalileriBulAsync(cafeId)
            };

            return View(viewModel);
        }
    }
}
