using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class Siparis
    {
        [Key]
        public int Id { get; set; }

        // Sipariş hangi şubeden geldiyse onu tutuyoruz.
        public int SubeId { get; set; }

        [ForeignKey("SubeId")]
        public Sube? Sube { get; set; }

        [Required]
        [StringLength(20)]
        public string? MasaNo { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ToplamTutar { get; set; }

        public DateTime SiparisTarihi { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(30)]
        public string Durum { get; set; } = "Bekliyor";

        public ICollection<SiparisDetay> Detaylar { get; set; } = new List<SiparisDetay>();
    }
}
