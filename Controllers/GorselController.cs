using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartMenu.Data;

namespace SmartMenu.Controllers
{
    // Veritabanında saklanan görselleri sunar. Müşteri menüsünde de gösterildiği için herkese açıktır.
    [AllowAnonymous]
    public class GorselController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GorselController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Her yüklemede yeni bir Id üretildiği için bir görselin içeriği asla değişmez;
        // tarayıcının bir yıl önbelleğe almasında sakınca yok.
        [HttpGet("/gorsel/{id:guid}")]
        [ResponseCache(Duration = 31536000, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Getir(Guid id)
        {
            var gorsel = await _context.Gorseller
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gorsel == null)
            {
                return NotFound();
            }

            return File(gorsel.Veri, gorsel.IcerikTipi);
        }
    }
}
