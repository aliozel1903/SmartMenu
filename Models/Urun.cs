using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartMenu.Models
{
    public class Urun
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ürün adı zorunludur.")]
        [StringLength(100)]
        public string? Ad { get; set; } // <-- Soru işareti eklendi, böylece null olabilir hale geldi

        [StringLength(500)]
        public string? Icerik { get; set; } 
        // (Seneye yapay zeka API'si burayı otomatik dolduracak!)

// parasal değerler için float yerine decimal kullanıyoruz 
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Fiyat { get; set; }

        [StringLength(255)]
        public string? ResimUrl { get; set; } // Fotoğrafın bilgisayardaki yolunu tutacak

        // Ürün müşteri menüsünde görünsün mü? Varsayılan true, yani yeni eklenen ürünler aktif başlar.
        public bool AktifMi { get; set; } = true;


// Bu ürün hangi kategoriye ait, bunu belirten foreign key
        public int KategoriId { get; set; }

        [ValidateNever]
        [ForeignKey("KategoriId")]
        public Kategori? Kategori { get; set; }

        // Bu ürün sepete eklenince müşteriye "yanına iyi gider" diye önerilen ürünler.
        [ValidateNever]
        public ICollection<UrunEslesme> Eslesmeler { get; set; } = new List<UrunEslesme>();
    }
}
