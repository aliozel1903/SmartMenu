using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SmartMenu.Models;
using Microsoft.AspNetCore.Authorization;
using SmartMenu.Data;

namespace SmartMenu.Controllers;

//[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        // Ana sayfa artık kafeleri listelemiyor: liste tüm şubelerin QR token'larını herkese açıyordu
        // ve kayıt açık olduğu için uygunsuz kafe adları ana sayfada görünebiliyordu.

        // "Canlı Demoyu Gör" butonu, ayarlarda bir demo şubenin QR token'ı tanımlıysa görünür
        // (canlı ortamda Demo__MenuToken ortam değişkeni). Token gerçekten var olan bir şubeye ait
        // değilse buton gösterilmez; ziyaretçi bozuk bir linke tıklamasın.
        var demoToken = _configuration["Demo:MenuToken"];
        if (!string.IsNullOrWhiteSpace(demoToken) && await _context.Subeler.AnyAsync(s => s.QrToken == demoToken))
        {
            ViewBag.DemoMenuUrl = Url.Action("Index", "Menu", new { token = demoToken });
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
