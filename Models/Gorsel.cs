using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    // Yüklenen görseller (ürün resmi, logo) diskte değil veritabanında tutulur.
    // Sebep: bulut sunucularında (Render, Railway vb.) disk kalıcı değildir;
    // uygulama yeniden başladığında wwwroot'a yazılan dosyalar silinir.
    public class Gorsel
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(50)]
        public string IcerikTipi { get; set; } = string.Empty;

        [Required]
        public byte[] Veri { get; set; } = Array.Empty<byte>();

        public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

        // Görselin boyutu (bayt). Kota hesaplarken büyük Veri sütununu okumamak için ayrıca tutulur.
        public int Boyut { get; set; }

        // Görseli yükleyen kafe. Kafe silinince görselleri de silinir (bkz. ApplicationDbContext);
        // böylece kötüye kullanılan bir hesap silindiğinde kapladığı yer de geri kazanılır.
        // Bu alan eklenmeden önce yüklenmiş görsellerde boştur.
        public int? CafeId { get; set; }

        [ForeignKey("CafeId")]
        public Cafe? Cafe { get; set; }
    }
}
