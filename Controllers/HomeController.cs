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

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // Ana sayfa araması için şubeleri marka adıyla birlikte getiriyoruz.
        ViewBag.SearchItems = await _context.Subeler
            .Include(s => s.Cafe)
            .OrderBy(s => s.SubeAdi)
            .Select(s => new
            {
                // Ana sayfa aramasında da şube Id'sini dışarı vermiyoruz, QR token'ını kullanıyoruz.
                token = s.QrToken,
                subeAdi = s.SubeAdi,
                markaAdi = s.Cafe != null ? s.Cafe.Ad : "",
                adres = s.Adres
            })
            .ToListAsync();

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
