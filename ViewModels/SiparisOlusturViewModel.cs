using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartMenu.ViewModels
{
    public class SiparisOlusturViewModel
    {
        // Şube Id'si yerine QR token. Böylece istemci "başka bir şubeye sipariş gönderme" denemesi yapamaz.
        [Required]
        [StringLength(64)]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string? MasaNo { get; set; }

        public List<SiparisKalemViewModel> Urunler { get; set; } = new List<SiparisKalemViewModel>();
    }

    public class SiparisKalemViewModel
    {
        public int UrunId { get; set; }
        public int Adet { get; set; }
    }
}
