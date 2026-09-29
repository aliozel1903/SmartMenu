using System;
using System.ComponentModel.DataAnnotations;

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
    }
}
