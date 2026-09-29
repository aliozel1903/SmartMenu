using System.Collections.Generic;
using SmartMenu.Models;

namespace SmartMenu.ViewModels
{
    public class MenuViewModel
    {
        // Menü ekranının üst kısmında kafe adı/adres gibi bilgileri göstermek için kullanılır.
        public Cafe Cafe { get; set; } = new Cafe();

        // QR hangi şubeye aitse müşteri ekranının tepesinde o şubenin adını gösteriyoruz.
        public Sube? Sube { get; set; }

        // Bu liste aktif kategorileri ve Include ile yüklenen ürünleri birlikte taşır.
        public List<Kategori> Kategoriler { get; set; } = new List<Kategori>();
    }
}
