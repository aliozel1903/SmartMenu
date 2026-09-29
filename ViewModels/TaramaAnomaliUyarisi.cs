namespace SmartMenu.ViewModels
{
    /// <summary>
    /// Bir şubenin QR tarama sayısında tespit edilen ani düşüşü temsil eder.
    /// </summary>
    public class TaramaAnomaliUyarisi
    {
        public int SubeId { get; set; }

        public string SubeAdi { get; set; } = string.Empty;

        // Dün kaç kez okutuldu.
        public int DunkuTarama { get; set; }

        // Önceki günlerin günlük ortalaması (normal davranış).
        public double NormalOrtalama { get; set; }

        // Normale göre yüzde kaç düştüğü.
        public int DususYuzdesi { get; set; }
    }
}
