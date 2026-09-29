using System.Collections.Generic;

namespace SmartMenu.ViewModels
{
    public class DashboardViewModel
    {
        public int ToplamUrun { get; set; }
        public int ToplamKategori { get; set; }
        public int BugunZiyaret { get; set; }

        // Chart.js labels alanı için son 7 günün kısa isimleri.
        public List<string> HaftalikZiyaretEtiketleri { get; set; } = new List<string>();

        // Chart.js data alanı için son 7 günün ziyaret sayıları.
        public List<int> HaftalikZiyaretVerileri { get; set; } = new List<int>();

        // QR taramalarında ani düşüş görülen şubeler. Boşsa panelde uyarı gösterilmez.
        public List<TaramaAnomaliUyarisi> QrTaramaUyarilari { get; set; } = new List<TaramaAnomaliUyarisi>();
    }
}
