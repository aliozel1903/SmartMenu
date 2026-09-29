using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class MasaBildirim
    {
        [Key]
        public int Id { get; set; }

        // Bildirim hangi şubeden geldiyse onu tutuyoruz.
        public int SubeId { get; set; }

        [ForeignKey("SubeId")]
        public Sube? Sube { get; set; }

        [Required]
        [StringLength(20)]
        public string MasaNo { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string BildirimTipi { get; set; } = string.Empty;

        public DateTime OlusturulmaTarihi { get; set; } = DateTime.UtcNow;

        // Personel bildirimi gördüğünde true yapılacak; spam kontrolü sadece okunmamış kayıtları dikkate alır.
        public bool OkunduMu { get; set; } = false;
    }
}
