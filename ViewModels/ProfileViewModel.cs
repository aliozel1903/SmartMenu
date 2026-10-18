using System.ComponentModel.DataAnnotations;

namespace SmartMenu.ViewModels
{
    public class ProfileViewModel
    {
        [Required(ErrorMessage = "Marka adı zorunludur.")]
        [StringLength(100)]
        public string? Ad { get; set; }

        // Sadece görüntüleme amaçlı. E-posta giriş ve şifre sıfırlama için kullanıldığından buradan değiştirilemez:
        // değiştirilebilseydi oturumu ele geçiren biri adresi kendi adresiyle değiştirip hesabı kalıcı olarak alabilirdi.
        public string? Email { get; set; }

        // Boş bırakılırsa mevcut şifre korunur.
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Yeni şifre en az 8 karakter olmalı.")]
        public string? YeniSifre { get; set; }

        // Şifre değiştirirken zorunlu: açık kalmış bir oturumu kullanan başkası şifreyi değiştiremesin.
        [DataType(DataType.Password)]
        public string? MevcutSifre { get; set; }

        // Sadece görüntüleme amaçlı: mevcut logonun yolu. Formdan gelmez, sunucudan doldurulur.
        public string? LogoUrl { get; set; }
    }
}
