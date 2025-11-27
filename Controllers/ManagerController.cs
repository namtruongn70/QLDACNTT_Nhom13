using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.ViewModel;
using CinemaManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CinemaManagement.Controllers
{
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public class ManagerController : Controller
    {
        private readonly AppDbContext _context;

        public ManagerController(AppDbContext context)
        {
            _context = context;
        }

        // Thay vì ViewBag.Theaters, bây giờ sẽ dùng ViewBag.Rooms để lấy danh sách phòng chiếu
        private void SetDropdownData()
        {
            ViewBag.Rooms = _context.Rooms
                .Include(r => r.Theater)  // nếu cần info rạp trong view
                .ToList();

            ViewBag.Genres = _context.Genres.ToList();
        }

        // Trang danh sách phim kèm lọc theo phòng (room) thay vì rạp (theater)
        public async Task<IActionResult> Index(int? roomId, string search)
        {
            SetDropdownData();

            var movies = _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes).ThenInclude(s => s.Room).ThenInclude(r => r.Theater)
                .AsQueryable();

            if (roomId.HasValue)
                movies = movies.Where(m => m.Showtimes.Any(s => s.RoomId == roomId.Value));

            if (!string.IsNullOrWhiteSpace(search))
                movies = movies.Where(m => m.Title.Contains(search));

            var vm = new TheaterMoviesViewModel
            {
                Movies = await movies.ToListAsync(),
                RoomId = roomId,
                RoomName = roomId.HasValue
                    ? (await _context.Rooms.Include(r => r.Theater).FirstOrDefaultAsync(r => r.RoomId == roomId.Value))?.Name
                    : null,
                SearchQuery = search,
                IsFiltered = roomId.HasValue || !string.IsNullOrWhiteSpace(search),
                Rooms = await _context.Rooms.Include(r => r.Theater).ToListAsync()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            SetDropdownData();

            var vm = new MovieFormViewModel
            {
                Movie = new Movie() { ReleaseDate = DateTime.Today },
                Genres = await _context.Genres.ToListAsync(),
                Rooms = await _context.Rooms.Include(r => r.Theater).ToListAsync(),
                Theaters = await _context.Theaters.ToListAsync(),
                SelectedGenres = new List<int>(),
                SelectedRoomId = new List<int>(),
                SelectedTheaterId = new List<int>(),
                StartTime = DateTime.Now.AddHours(1)
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create(MovieFormViewModel vm)
        {
            SetDropdownData();

            if (!ModelState.IsValid)
            {
                vm.Genres = await _context.Genres.ToListAsync();
                vm.Rooms = await _context.Rooms.Include(r => r.Theater).ToListAsync();
                return View(vm);
            }

            if (vm.SelectedRoomId != null && vm.SelectedRoomId.Any())
            {
                foreach (var roomId in vm.SelectedRoomId.Distinct())
                {
                    _context.Showtimes.Add(new Showtime
                    {
                        MovieId = vm.Movie.MovieId,
                        RoomId = roomId,
                        StartTime = vm.StartTime ?? DateTime.Now.AddHours(1)
                    });
                }
            }

            _context.Movies.Add(vm.Movie);
            await _context.SaveChangesAsync();

            if (vm.SelectedGenres != null)
            {
                foreach (var gid in vm.SelectedGenres.Distinct())
                {
                    _context.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = vm.Movie.MovieId,
                        GenreId = gid
                    });
                }
            }

            foreach (var roomId in vm.SelectedRoomId.Distinct())
            {
                _context.Showtimes.Add(new Showtime
                {
                    MovieId = vm.Movie.MovieId,
                    RoomId = roomId,
                    StartTime = vm.StartTime ?? DateTime.Now.AddHours(1)
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            SetDropdownData();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .Include(m => m.Showtimes).ThenInclude(s => s.Room)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            var vm = new MovieFormViewModel
            {
                Movie = movie,
                Genres = await _context.Genres.ToListAsync(),
                SelectedGenres = movie.MovieGenres.Select(mg => mg.GenreId).ToList(),
                Rooms = await _context.Rooms.Include(r => r.Theater).ToListAsync(),
                SelectedRoomId = movie.Showtimes.Select(s => s.RoomId).Distinct().ToList(),
                StartTime = movie.Showtimes.FirstOrDefault()?.StartTime ?? DateTime.Now.AddHours(1),
                Theaters = await _context.Theaters.ToListAsync(), // ✅ Thêm dòng này
                SelectedTheaterId = movie.Showtimes.Select(s => s.Room.TheaterId).Distinct().ToList() // Nếu bạn cần đánh dấu checkbox
            };


            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, MovieFormViewModel vm)
        {
            SetDropdownData();

            if (id != vm.Movie.MovieId) return NotFound();

            if (!ModelState.IsValid)
            {
                vm.Genres = await _context.Genres.ToListAsync();
                vm.Rooms = await _context.Rooms.Include(r => r.Theater).ToListAsync();
                return View(vm);
            }

            var movieInDb = await _context.Movies
                .Include(m => m.MovieGenres)
                .Include(m => m.Showtimes).ThenInclude(s => s.Room)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movieInDb == null) return NotFound();

            movieInDb.Title = vm.Movie.Title;
            movieInDb.Description = vm.Movie.Description;
            movieInDb.Duration = vm.Movie.Duration;
            movieInDb.PosterUrl = vm.Movie.PosterUrl;
            movieInDb.VideoUrl = vm.Movie.VideoUrl;
            movieInDb.ReleaseDate = vm.Movie.ReleaseDate;
            //movieInDb.Price = vm.Movie.Price;

            _context.MovieGenres.RemoveRange(movieInDb.MovieGenres);
            if (vm.SelectedGenres != null)
            {
                foreach (var gid in vm.SelectedGenres.Distinct())
                {
                    _context.MovieGenres.Add(new MovieGenre
                    {
                        MovieId = id,
                        GenreId = gid
                    });
                }
            }

            var existingShowtimes = movieInDb.Showtimes.ToList();
            var selectedRoomIds = vm.SelectedRoomId.Distinct().ToList();

            // Xóa những suất chiếu không còn trong chọn phòng
            foreach (var st in existingShowtimes.Where(st => !selectedRoomIds.Contains(st.RoomId)))
                _context.Showtimes.Remove(st);

            var remainRooms = existingShowtimes.Select(st => st.RoomId).ToHashSet();

            // Thêm suất chiếu cho các phòng mới được chọn
            foreach (var roomId in selectedRoomIds)
            {
                if (!remainRooms.Contains(roomId))
                {
                    _context.Showtimes.Add(new Showtime
                    {
                        MovieId = id,
                        RoomId = roomId,
                        StartTime = vm.StartTime ?? DateTime.Now.AddHours(1)
                    });
                }
            }

            // Cập nhật lại thời gian bắt đầu cho các suất chiếu còn lại
            foreach (var st in movieInDb.Showtimes)
            {
                st.StartTime = vm.StartTime ?? st.StartTime;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            SetDropdownData();

            var movie = await _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes).ThenInclude(s => s.Room).ThenInclude(r => r.Theater)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Showtimes).ThenInclude(s => s.Tickets) // include vé
                .Include(m => m.MovieGenres)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            // Xóa vé trước
            foreach (var showtime in movie.Showtimes)
            {
                if (showtime.Tickets != null && showtime.Tickets.Any())
                {
                    _context.Tickets.RemoveRange(showtime.Tickets);
                }
            }

            // Xóa các suất chiếu
            if (movie.Showtimes != null && movie.Showtimes.Any())
            {
                _context.Showtimes.RemoveRange(movie.Showtimes);
            }

            // Xóa các thể loại
            if (movie.MovieGenres != null && movie.MovieGenres.Any())
            {
                _context.MovieGenres.RemoveRange(movie.MovieGenres);
            }

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
