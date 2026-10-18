using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartMenu.Models
{
    public enum EpostaTokenAmaci
    {
        EpostaDogrulama = 0,
        SifreSifirlama = 1
    }

    /// <summary>
    /// E-postayla gönderilen tek kullanımlık bağlantıların (doğrulama, şifre sıfırlama) kaydı.
    ///
    /// Token'ın kendisi değil SHA-256 özeti saklanır: veritabanı sızsa bile içindeki özetlerden
    /// çalışan bir bağlantı üretilemez. Aynı mantıkla şifreleri de düz metin değil BCrypt özeti olarak tutuyoruz.
    /// </summary>
    public class EpostaToken
    {
        [Key]
        public int Id { get; set; }

        public int CafeId { get; set; }

        [ForeignKey("CafeId")]
        public Cafe? Cafe { get; set; }

        public EpostaTokenAmaci Amac { get; set; }

        [Required]
        [StringLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime OlusturulmaTarihi { get; set; }

        public DateTime SonKullanmaTarihi { get; set; }

        // Bağlantı kullanıldığında ya da yerine yenisi gönderildiğinde dolar; dolu olan token bir daha çalışmaz.
        public DateTime? KullanilmaTarihi { get; set; }
    }
}
