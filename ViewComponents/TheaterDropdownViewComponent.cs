using CinemaManagement.Data;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace CinemaManagement.ViewComponents
{
    public class TheaterDropdownViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;

        public TheaterDropdownViewComponent(AppDbContext context)
        {
            _context = context;
        }

        public IViewComponentResult Invoke()
        {
            var theaters = _context.Theaters.ToList();
            return View(theaters);
        }
    }
}
