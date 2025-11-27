using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace CinemaManagement.Controllers
{
    public class ShowtimesController : Controller
    {
        private readonly AppDbContext _context;

        public ShowtimesController(AppDbContext context)
        {
            _context = context ?? throw new System.ArgumentNullException(nameof(context));
        }

        // Danh sách suất chiếu theo phim (movieId optional)
        public IActionResult Index(int? movieId)
        {
            var showtimes = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .AsQueryable();

            if (movieId.HasValue)
            {
                ViewBag.MovieId = movieId.Value;
                showtimes = showtimes.Where(s => s.MovieId == movieId.Value);
                ViewBag.MovieTitle = _context.Movies.FirstOrDefault(m => m.MovieId == movieId.Value)?.Title;
            }

            return View(showtimes.ToList());
        }

        // Chi tiết suất chiếu
        public IActionResult Details(int id)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .FirstOrDefault(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound();

            return View(showtime);
        }
        // GET: Thêm suất chiếu
        [HttpGet]
        public IActionResult Create(int movieId)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            if (movie == null)
                return NotFound();

            var theaters = _context.Theaters.ToList() ?? new List<Theater>();

            ViewBag.MovieId = movieId;
            ViewBag.MovieTitle = movie.Title;
            ViewBag.Theaters = _context.Theaters.ToList();
            ViewBag.RoomList = new SelectList(new List<Room>(), "RoomId", "Name"); // ban đầu chưa chọn rạp nên không có phòng

            return View();
        }

        // POST: Thêm suất chiếu
        [HttpPost]
        public IActionResult Create(Showtime showtime)
        {
            if (ModelState.IsValid)
            {
                // Không áp dụng giảm giá ở đây, chỉ lưu giá gốc
                _context.Showtimes.Add(showtime);
                _context.SaveChanges();

                TempData["Success"] = "✅ Đã thêm suất chiếu mới!";
                return RedirectToAction("Index", new { movieId = showtime.MovieId });
            }

            // Nếu ModelState không hợp lệ, load lại dropdown cho rạp và phòng theo phòng đã chọn (nếu có)
            var room = _context.Rooms.Include(r => r.Theater).FirstOrDefault(r => r.RoomId == showtime.RoomId);
            var theaterId = room?.TheaterId;

            var theaters = _context.Theaters.ToList() ?? new List<Theater>();
            var rooms = theaterId.HasValue ? _context.Rooms.Where(r => r.TheaterId == theaterId).ToList() : new List<Room>();

            ViewBag.MovieId = showtime.MovieId;
            ViewBag.MovieTitle = _context.Movies.FirstOrDefault(m => m.MovieId == showtime.MovieId)?.Title;
            ViewBag.TheaterList = new SelectList(theaters, "TheaterId", "Name", theaterId);
            ViewBag.RoomList = new SelectList(rooms, "RoomId", "Name", showtime.RoomId);

            return View(showtime);
        }

        // GET: Sửa suất chiếu
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Room)
                .ThenInclude(r => r.Theater)
                .FirstOrDefault(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound();

            var viewModel = new ShowtimeViewModel
            {
                ShowtimeId = showtime.ShowtimeId,
                MovieId = showtime.MovieId,
                RoomId = showtime.RoomId,
                TheaterId = showtime.Room?.TheaterId ?? 0,
                StartTime = showtime.StartTime,
                Price = showtime.Price,
            };

            var theaters = _context.Theaters.ToList();
            var rooms = _context.Rooms.Where(r => r.TheaterId == viewModel.TheaterId).ToList();

            ViewBag.MovieId = viewModel.MovieId;
            ViewBag.MovieTitle = _context.Movies.FirstOrDefault(m => m.MovieId == viewModel.MovieId)?.Title;
            ViewBag.TheaterList = new SelectList(theaters, "TheaterId", "Name", viewModel.TheaterId);
            ViewBag.RoomList = new SelectList(rooms, "RoomId", "Name", viewModel.RoomId);

            return View(viewModel);
        }

        // POST: Sửa suất chiếu
        [HttpPost]
        public IActionResult Edit(ShowtimeViewModel vm)
        {
            if (ModelState.IsValid)
            {
                var showtime = _context.Showtimes.FirstOrDefault(s => s.ShowtimeId == vm.ShowtimeId);
                if (showtime == null)
                    return NotFound();

                showtime.StartTime = vm.StartTime;
                showtime.RoomId = vm.RoomId;
                showtime.MovieId = vm.MovieId;
                showtime.Price = vm.Price;

                showtime.DiscountPercent = vm.DiscountPercent;
                showtime.DiscountStart = vm.DiscountStart;
                showtime.DiscountEnd = vm.DiscountEnd;

                _context.SaveChanges();

                TempData["Success"] = "✅ Cập nhật thành công!";
                return RedirectToAction("Index", new { movieId = vm.MovieId });
            }

            var theaters = _context.Theaters.ToList();
            var rooms = _context.Rooms.Where(r => r.TheaterId == vm.TheaterId).ToList();

            ViewBag.MovieId = vm.MovieId;
            ViewBag.MovieTitle = _context.Movies.FirstOrDefault(m => m.MovieId == vm.MovieId)?.Title;
            ViewBag.TheaterList = new SelectList(theaters, "TheaterId", "Name", vm.TheaterId);
            ViewBag.RoomList = new SelectList(rooms, "RoomId", "Name", vm.RoomId);

            return View(vm);
        }

        // GET: Xác nhận xoá suất chiếu
        [HttpGet]
        public IActionResult Delete(int id)
        {
            var showtime = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room).ThenInclude(r => r.Theater)
                .FirstOrDefault(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound();

            return View(showtime);
        }

        // POST: Xác nhận xoá
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime != null)
            {
                _context.Showtimes.Remove(showtime);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: Lấy danh sách phòng theo rạp
        [HttpGet]
        public IActionResult GetRoomsByTheater(int theaterId)
        {
            var rooms = _context.Rooms
                .Where(r => r.TheaterId == theaterId)
                .Select(r => new { roomId = r.RoomId, name = r.Name })
                .ToList();

            return Json(rooms);
        }
    }
}
