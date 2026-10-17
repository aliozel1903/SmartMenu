using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Models;

namespace SmartMenu.Data
{
    // IDataProtectionKeyContext: giriş çerezlerini şifreleyen anahtarlar veritabanında saklanır.
    // Bulut sunucusunda disk kalıcı olmadığı için, anahtarlar bellekte kalsaydı her yeniden
    // başlatmada tüm yöneticilerin oturumu düşerdi.
    public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
    {
        // Sistem çalışırken veri tabanı ayarlarını (şifre, sunucu adresi vb.) bu constructor ile içeri alacağız
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // Veri tabanında oluşacak asıl tablolarımız (DbSet'ler)
        public DbSet<Cafe> Kafeler { get; set; }
        public DbSet<Kategori> Kategoriler { get; set; }
        public DbSet<Urun> Urunler { get; set; }
        public DbSet<MenuZiyaret> MenuZiyaretleri { get; set; }
        public DbSet<Sube> Subeler { get; set; }
        public DbSet<Siparis> Siparisler { get; set; }
        public DbSet<SiparisDetay> SiparisDetaylari { get; set; }
        public DbSet<MasaBildirim> MasaBildirimleri { get; set; }
        public DbSet<Gorsel> Gorseller { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Sipariş -> SiparişDetay bire-çok ilişkisi. Sipariş silinirse detayları da silinir.
            modelBuilder.Entity<Siparis>()
                .HasMany(s => s.Detaylar)
                .WithOne(d => d.Siparis)
                .HasForeignKey(d => d.SiparisId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SiparisDetay>()
                .HasOne(d => d.Urun)
                .WithMany()
                .HasForeignKey(d => d.UrunId)
                .OnDelete(DeleteBehavior.Restrict);

            // QR token'ı benzersiz olmalı: iki şube aynı token'a sahip olamaz.
            // Ayrıca token ile şube arama işlemi her müşteri menü açılışında çalıştığı için index performansı da sağlar.
            modelBuilder.Entity<Sube>()
                .HasIndex(s => s.QrToken)
                .IsUnique();

            // Yeni açılan şubelerde online sipariş varsayılan olarak açık gelir.
            modelBuilder.Entity<Sube>()
                .Property(s => s.IsSiparisAktif)
                .HasDefaultValue(true);

            modelBuilder.Entity<Sube>()
                .Property(s => s.GarsonCagriAktif)
                .HasDefaultValue(true);

            modelBuilder.Entity<Sube>()
                .Property(s => s.HesapIsteAktif)
                .HasDefaultValue(true);

            // Şube silinirse ziyaret istatistikleri tarihsel veri olarak kalsın (SubeId null'a düşer),
            // çünkü bu kayıtlar ileride trend/stok analizinde de kullanılacak.
            modelBuilder.Entity<MenuZiyaret>()
                .HasOne(z => z.Sube)
                .WithMany()
                .HasForeignKey(z => z.SubeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Günlük ziyaret kaydı her menü açılışında okunup güncelleniyor; bu index o sorguyu hızlandırır.
            modelBuilder.Entity<MenuZiyaret>()
                .HasIndex(z => new { z.CafeId, z.SubeId, z.Tarih });

            // Kafe silinince yüklediği görseller de silinir.
            modelBuilder.Entity<Gorsel>()
                .HasOne(g => g.Cafe)
                .WithMany()
                .HasForeignKey(g => g.CafeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Şube silinirse o şubeye ait masa bildirimleri de temizlenir.
            modelBuilder.Entity<MasaBildirim>()
                .HasOne(b => b.Sube)
                .WithMany()
                .HasForeignKey(b => b.SubeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
