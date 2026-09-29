using System;
using System.ComponentModel.DataAnnotations;

namespace SmartMenu.Models
{
    public class Cafe
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Kafe adı zorunludur.")]
        [StringLength(100)]
        public string? Ad { get; set; } // Sarı noktayı çözen sihirli '?' işareti

        [StringLength(250)]
        public string? Adres { get; set; } // Sarı noktayı çözen sihirli '?' işareti

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(150)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(255)]
        public string? SifreHash { get; set; }

        // İşletmenin logosu. QR kodun ortasında ve basılan masa kartında görünür.
        // Boşsa markanın baş harfleri kullanılır.
        [StringLength(255)]
        public string? LogoUrl { get; set; }

        public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;
    }
}
