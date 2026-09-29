using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class Sube
    {
        [Key]
        public int Id { get; set; }

        // Şube hangi merkeze/markaya bağlı, bunu CafeId ile tutuyoruz.
        public int CafeId { get; set; }

        [ForeignKey("CafeId")]
        public Cafe? Cafe { get; set; }

        [Required(ErrorMessage = "Şube adı zorunludur.")]
        [StringLength(100)]
        public string? SubeAdi { get; set; }

        [StringLength(30)]
        public string? Telefon { get; set; }

        [StringLength(250)]
        public string? Adres { get; set; }

        // QR kodun taşıdığı genel (public) kimlik. Sıralı Id yerine bunu kullanıyoruz ki
        // kimse adres çubuğunda 1, 2, 3 deneyerek başka şubelerin menüsüne ulaşamasın.
        // Kriptografik rastgele üretilir; sızarsa yönetici panelinden yenilenip eski QR iptal edilebilir.
        [Required]
        [StringLength(64)]
        public string QrToken { get; set; } = string.Empty;

        // QR kodun en son ne zaman yenilendiği. Yönetici "QR'ı Yenile" dediğinde güncellenir.
        public DateTime QrTokenTarihi { get; set; } = DateTime.UtcNow;

        // Bu kilit kapalıysa müşteri menüyü görür ama sepet/sipariş butonları gizlenir.
        public bool IsSiparisAktif { get; set; } = true;

        // Şube bazında müşteri menüsündeki Garson Çağır butonunu açıp kapatır.
        public bool GarsonCagriAktif { get; set; } = true;

        // Şube bazında müşteri menüsündeki Hesap İste butonunu açıp kapatır.
        public bool HesapIsteAktif { get; set; } = true;

        public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;
    }
}
