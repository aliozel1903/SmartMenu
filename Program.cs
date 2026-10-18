using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;
using SmartMenu.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// GÜVENLİK GÖREVLİSİ (Authentication) AYARLARI
builder.Services.AddAuthentication(OturumYoneticisi.Sema)
    .AddCookie(OturumYoneticisi.Sema, options =>
    {
        options.LoginPath = "/Account/Login"; // Biletsiz girmeye çalışanları bu sayfaya yolla
        options.ExpireTimeSpan = TimeSpan.FromDays(1); // 1 gün boyunca girişi hatırla
        // Şifre değişince diğer cihazlardaki oturumlar kapanır (güvenlik damgası kontrolü).
        options.Events.OnValidatePrincipal = OturumYoneticisi.DogrulaAsync;
    });

// PostgreSQL bağlantısı. Bağlantı dizesi (şifre dahil) depoda hiçbir dosyada tutulmaz:
//  - Geliştirmede: dotnet user-secrets (bilgisayara özel, proje klasörünün dışında)
//  - Canlı ortamda: "ConnectionStrings__DefaultConnection" ortam değişkeni
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Veritabanı bağlantısı tanımlı değil. Geliştirmede: " +
        "dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=localhost;Database=SmartMenuDb;Username=...;Password=...\" " +
        "Canlı ortamda: ConnectionStrings__DefaultConnection ortam değişkenini tanımlayın.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddRouting(options => options.LowercaseUrls = true); // URL'leri küçük harfe dönüştür

// Giriş çerezlerini şifreleyen anahtarlar veritabanında tutulur (bkz. ApplicationDbContext).
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("SmartMenu");

// İş mantığı servisleri. DbContext gibi istek başına (scoped) yaşarlar.
builder.Services.AddScoped<TaramaAnomaliServisi>();
builder.Services.AddScoped<GuvenliResimYukleyici>();
builder.Services.AddScoped<HesapTokenServisi>();
builder.Services.AddHttpClient<EpostaGonderici>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Hız sınırlama: aynı IP'den kısa sürede gelen aşırı istekleri 429 ile reddeder.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Müşteri menüsündeki sipariş ve garson/hesap çağırma uçları (giriş gerektirmez).
    options.AddPolicy("herkeseAcik", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Giriş ve kayıt: kaba kuvvet şifre denemesini yavaşlatır.
    options.AddPolicy("giris", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    // E-posta gönderen formlar (şifremi unuttum, doğrulama bağlantısını tekrar gönder).
    // Hesap başına ve günlük toplam sınırlar ayrıca HesapTokenServisi'nde.
    options.AddPolicy("eposta", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "bilinmiyor",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10)
            }));
});

// Barındırma sağlayıcıları (Render vb.) HTTPS'i önlerindeki proxy'de sonlandırıp isteği uygulamaya iletir.
// Bu ayar olmadan uygulama her isteği "http" ve proxy'nin IP'sinden gelmiş sanır: HTTPS yönlendirmesi
// döngüye girer ve hız sınırlama tüm müşterileri tek bir IP olarak sayar.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Proxy'nin IP adresi önceden bilinmediği için listeyi temizliyoruz. ForwardLimit varsayılan olarak 1'dir:
    // yalnızca proxy'nin eklediği en son IP'ye güvenilir, istemcinin sahte başlık göndermesi işe yaramaz.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Açılışta bekleyen veritabanı migration'larını uygular. Böylece canlı veritabanı (Neon)
// ayrıca elle güncellenmek zorunda kalmaz; yeni sürüm yayınlandığında şema kendiliğinden güncellenir.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication(); // Kimlik kontrolü (mutlaka Authorization'dan önce olmalı)
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
