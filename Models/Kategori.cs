using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class Kategori
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Kategori adı zorunludur.")]
        [StringLength(50)]
        public string? Ad { get; set; }

        [Display(Name = "Görünüm Sırası")]
        public int Sira { get; set; } = 0;

        [Display(Name = "Menüde Gösterilsin mi?")]
        public bool AktifMi { get; set; } = true;

        public int CafeId { get; set; }
        
        [ForeignKey("CafeId")]
        public Cafe? Cafe { get; set; }

        public ICollection<Urun>? Urunler { get; set; }
    }
}