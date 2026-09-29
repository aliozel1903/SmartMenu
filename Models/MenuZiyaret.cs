using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public class MenuZiyaret
    {
        [Key]
        public int Id { get; set; }

        // Hangi kafenin menüsü ziyaret edildi, bunu tutuyoruz.
        public int CafeId { get; set; }

        [ForeignKey("CafeId")]
        public Cafe? Cafe { get; set; }

        // Hangi şubenin QR'ı okutuldu. Şube bazlı tutmamızın sebebi: taramalar aniden düşerse
        // hangi şubenin masalarındaki QR kodunun kapatılmış/değiştirilmiş olabileceğini söyleyebilmek.
        // Bu alan eklenmeden önceki eski kayıtlarda null'dır.
        public int? SubeId { get; set; }

        [ForeignKey("SubeId")]
        public Sube? Sube { get; set; }

        // Saat/dakika değil, sadece gün bazında raporlama yapacağız.
        [Column(TypeName = "date")]
        public DateTime Tarih { get; set; }

        public int ZiyaretSayisi { get; set; }
    }
}
