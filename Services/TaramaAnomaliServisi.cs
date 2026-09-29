using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.ViewModels;

namespace SmartMenu.Services
{
    /// <summary>
    /// QR tarama sayılarındaki ani düşüşleri tespit eder.
    /// Amaç: masadaki QR kodun üzerine sahte bir etiket yapıştırılması ("quishing") ya da
    /// kodun sökülmesi/yıpranması durumunu erkenden fark etmek.
    ///
    /// Bu saldırı tamamen uygulamanın dışında gerçekleştiği için (müşteri sahte kodu okuttuğunda
    /// bizim sunucumuza hiç ulaşmaz) sunucu tarafında yapılabilecek tek gerçek savunma,
    /// "beklenen trafiğin kaybolduğunu" görüp işletmeyi uyarmaktır.
    /// </summary>
    public class TaramaAnomaliServisi
    {
        private readonly ApplicationDbContext _context;

        // Karşılaştırma için kaç günlük normal davranış temel alınsın.
        private const int TemelGunSayisi = 7;

        // Dünkü tarama, normalin bu oranının altına düşerse uyarı üretilir (%40).
        private const double DususEsigi = 0.4;

        // Çok az trafiği olan şubelerde tek bir sessiz gün bile yüzde olarak büyük düşüş gösterir.
        // Bu yüzden gürültüyü engellemek için normalde en az bu kadar tarama bekliyoruz.
        private const double AnlamliTrafikEsigi = 5.0;

        public TaramaAnomaliServisi(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaramaAnomaliUyarisi>> AnomalileriBulAsync(int cafeId)
        {
            // Bugünü kasten dışarıda bırakıyoruz: gün henüz bitmediği için sabah saatlerinde
            // her şube "düşmüş" gibi görünür ve sistem sürekli yanlış alarm verirdi.
            var dun = DateTime.Today.AddDays(-1);
            var temelBaslangic = dun.AddDays(-TemelGunSayisi);

            var kayitlar = await _context.MenuZiyaretleri
                .Include(z => z.Sube)
                .Where(z => z.CafeId == cafeId
                    && z.SubeId != null
                    && z.Tarih >= temelBaslangic
                    && z.Tarih <= dun)
                .ToListAsync();

            var uyarilar = new List<TaramaAnomaliUyarisi>();

            foreach (var subeGrubu in kayitlar.GroupBy(z => z.SubeId!.Value))
            {
                var dunkuTarama = subeGrubu.FirstOrDefault(z => z.Tarih == dun)?.ZiyaretSayisi ?? 0;

                // Temel (normal) davranış: dünden önceki günler. Dün bu hesaba dahil edilmez,
                // aksi halde düşüşün kendisi ortalamayı aşağı çekip anomaliyi gizlerdi.
                var oncekiGunler = subeGrubu.Where(z => z.Tarih < dun).ToList();
                if (oncekiGunler.Count == 0)
                {
                    continue;
                }

                // Kayıt olmayan günler 0 ziyaret demektir; ortalamayı sabit gün sayısına bölüyoruz.
                var normalOrtalama = oncekiGunler.Sum(z => z.ZiyaretSayisi) / (double)TemelGunSayisi;

                if (normalOrtalama < AnlamliTrafikEsigi)
                {
                    continue;
                }

                if (dunkuTarama > normalOrtalama * DususEsigi)
                {
                    continue;
                }

                uyarilar.Add(new TaramaAnomaliUyarisi
                {
                    SubeId = subeGrubu.Key,
                    SubeAdi = subeGrubu.First().Sube?.SubeAdi ?? "Bilinmeyen şube",
                    DunkuTarama = dunkuTarama,
                    NormalOrtalama = Math.Round(normalOrtalama, 1),
                    DususYuzdesi = (int)Math.Round((1 - (dunkuTarama / normalOrtalama)) * 100)
                });
            }

            // En sert düşüş en üstte görünsün.
            return uyarilar.OrderByDescending(u => u.DususYuzdesi).ToList();
        }
    }
}
