using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class SiparisDetay
    {
        [Key]
        public int Id { get; set; }

        public int SiparisId { get; set; }

        [ForeignKey("SiparisId")]
        public Siparis? Siparis { get; set; }

        public int UrunId { get; set; }

        [ForeignKey("UrunId")]
        public Urun? Urun { get; set; }

        public int Adet { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BirimFiyat { get; set; }
    }
}
