using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SmartMenu.Models
{
    // "Birlikte iyi gider": kafe sahibi bir ürünün yanına önerilecek ürünleri kendisi seçer
    // (ör. Türk Kahvesi -> Lokum). Müşteri ürünü sepete ekleyince bu öneriler gösterilir.
    // Tek yönlüdür: kahvenin önerisi lokumsa, lokumun önerisi ayrıca seçilmedikçe kahve olmaz.
    public class UrunEslesme
    {
        // Menü kalabalıklaşmasın ve müşteri bunaltılmasın diye ürün başına öneri sınırı.
        public const int EnFazlaOneri = 3;

        public int UrunId { get; set; }

        [ValidateNever]
        public Urun? Urun { get; set; }

        public int OnerilenUrunId { get; set; }

        [ValidateNever]
        public Urun? OnerilenUrun { get; set; }
    }
}
