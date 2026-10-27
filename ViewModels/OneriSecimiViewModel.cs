using System.Collections.Generic;
using SmartMenu.Models;

namespace SmartMenu.ViewModels
{
    // Ürün ekleme/düzenleme formundaki "Birlikte iyi gider" seçim alanının verisi.
    public class OneriSecimiViewModel
    {
        // Seçilebilecek ürünler: kafenin diğer ürünleri, kategorileriyle birlikte.
        public List<Urun> Adaylar { get; set; } = new List<Urun>();

        // Şu an işaretli olan ürünlerin Id'leri.
        public HashSet<int> Secili { get; set; } = new HashSet<int>();
    }
}
