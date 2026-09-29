using System.ComponentModel.DataAnnotations;

namespace SmartMenu.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Marka adı zorunludur.")]
        [StringLength(100)]
        public string? Ad { get; set; }

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(150)]
        public string? Email { get; set; }

        // Boş bırakılırsa mevcut şifre korunur.
        [DataType(DataType.Password)]
        public string? YeniSifre { get; set; }

        // Sadece görüntüleme amaçlı: mevcut logonun yolu. Formdan gelmez, sunucudan doldurulur.
        public string? LogoUrl { get; set; }
    }
}
