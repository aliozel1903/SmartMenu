using System.ComponentModel.DataAnnotations;

namespace SmartMenu.ViewModels
{
    public class MasaBildirimViewModel
    {
        // Artık istemciden şube Id'si almıyoruz. Müşteri sadece QR'dan gelen token'ı gönderir,
        // hangi şubeye ait olduğunu sunucu kendi veritabanından çözer.
        [Required]
        [StringLength(64)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string MasaNo { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string BildirimTipi { get; set; } = string.Empty;
    }
}
