using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Models;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CinemaManagement.ViewModel;
using Microsoft.AspNetCore.Identity;

namespace CinemaManagement.Controllers
{
    public class MoviesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MoviesController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        protected void SetCommonData()
        {
            ViewBag.Theaters = _context.Theaters.ToList();
            ViewBag.Genres = _context.Genres.ToList();
        }

        // Trang Movies
        public async Task<IActionResult> Index(string searchQuery, int? theaterId, string sortBy, int? genreId)
        {
            var user = await _userManager.GetUserAsync(User);

            IList<string> roles = new List<string>();
            if (user != null)
            {
                roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Users");
                }
            }

            SetCommonData();

            // Danh sách giờ chiếu dùng cho dropdown
            ViewBag.ShowtimeHours = _context.Showtimes
                .Select(s => s.StartTime.Hour)
                .Distinct()
                .OrderBy(h => h)
                .ToList();

            var movies = _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Theater)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
                movies = movies.Where(m => m.Title.Contains(searchQuery));

            if (genreId.HasValue)
                movies = movies.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId));

            if (theaterId.HasValue)
                movies = movies.Where(m => m.Showtimes.Any(s => s.Room.TheaterId == theaterId));

            switch (sortBy)
            {
                case "title_asc": movies = movies.OrderBy(m => m.Title); break;
                case "title_desc": movies = movies.OrderByDescending(m => m.Title); break;
                case "rating_asc": movies = movies.OrderBy(m => m.Rating); break;
                case "rating_desc": movies = movies.OrderByDescending(m => m.Rating); break;
            }

            ViewData["SearchQuery"] = searchQuery;
            // Trong Index của MoviesController, trước khi return View(model);
            ViewBag.TopRatedMovies = await _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .OrderByDescending(m => m.Rating)
                .Take(3)
                .ToListAsync();

            var model = new TheaterMoviesViewModel
            {
                Movies = await movies.ToListAsync(),
                IsFiltered = theaterId.HasValue,
                TheaterName = theaterId.HasValue
                    ? await _context.Theaters.Where(t => t.TheaterId == theaterId).Select(t => t.Name).FirstOrDefaultAsync()
                    : null
            };

            return View(model);
        }

        // Lọc phim bằng Ajax
        [HttpGet]
        public IActionResult FilterAjax(string searchQuery, int? theaterId, int? genreId, int? time)
        {
            var movies = _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Theater)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
                movies = movies.Where(m => m.Title.Contains(searchQuery));

            if (genreId.HasValue)
                movies = movies.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId.Value));

            if (theaterId.HasValue)
                movies = movies.Where(m => m.Showtimes.Any(s => s.Room.TheaterId == theaterId.Value));

            if (time.HasValue)
                movies = movies.Where(m => m.Showtimes.Any(s => s.StartTime.Hour == time.Value));

            return PartialView("_MovieCardsPartial", movies.ToList());
        }

        public IActionResult Details(int id)
        {
            var movie = _context.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.Showtimes)
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Theater)
                .Include(m => m.MovieRatings)
                .FirstOrDefault(m => m.MovieId == id);

            if (movie == null)
            {
                TempData["Error"] = "❌ Phim không tồn tại!";
                return RedirectToAction("Index");
            }

            HttpContext.Session.SetInt32("MovieId", id);
            return View(movie);
        }

        public IActionResult Watch(int id)
        {
            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == id);
            if (movie == null)
            {
                TempData["Error"] = "❌ Phim không tồn tại!";
                return RedirectToAction("Index");
            }

            ViewBag.MovieEmbedUrl = movie.EmbedVideoUrl;
            return View();
        }

        public IActionResult Showtimes(int movieId, int? theaterId)
        {
            var showtimesQuery = _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Theater)
                .Where(s => s.MovieId == movieId);

            if (theaterId.HasValue)
                showtimesQuery = showtimesQuery.Where(s => s.Room.TheaterId == theaterId.Value);

            var showtimes = showtimesQuery.ToList();

            if (!showtimes.Any())
            {
                TempData["Error"] = "⛔ Không có suất chiếu khả dụng.";
                return RedirectToAction("Index");
            }

            return View(showtimes);
        }

        [HttpPost]
        public IActionResult RateMovie(int movieId, int rating)
        {
            if (rating < 1 || rating > 10)
            {
                TempData["Error"] = "Đánh giá không hợp lệ.";
                return RedirectToAction("Details", new { id = movieId });
            }

            var movie = _context.Movies.FirstOrDefault(m => m.MovieId == movieId);
            if (movie == null)
                return NotFound();

            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                TempData["Error"] = "Vui lòng đăng nhập để đánh giá.";
                return RedirectToAction("Details", new { id = movieId });
            }

            var existingRating = _context.MovieRatings
                .FirstOrDefault(r => r.MovieId == movieId && r.UserId == user.Id);

            if (existingRating != null)
            {
                existingRating.Rating = rating;
                existingRating.RatedAt = DateTime.Now;
            }
            else
            {
                _context.MovieRatings.Add(new MovieRating
                {
                    MovieId = movieId,
                    UserId = user.Id,
                    Rating = rating,
                    RatedAt = DateTime.Now
                });
            }

            _context.SaveChanges();

            var avgRating = _context.MovieRatings
                .Where(r => r.MovieId == movieId)
                .Average(r => r.Rating);

            movie.Rating = Math.Round(avgRating, 1);
            _context.SaveChanges();

            TempData["Success"] = "Cảm ơn bạn đã đánh giá!";
            return RedirectToAction("Details", new { id = movieId });
        }

        // CRUD phim (Create, Edit)
        [HttpGet]
        public IActionResult Create()
        {
            SetCommonData();
            var model = new MovieFormViewModel
            {
                Movie = new Movie(),
                Genres = _context.Genres.ToList(),
                Theaters = _context.Theaters.ToList(),
                SelectedGenres = new List<int>(),
                SelectedTheaterId = new List<int>()
            };
            return View("~/Views/Manager/Create.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(MovieFormViewModel model, List<int> SelectedGenres, List<int> SelectedRoomIds)
        {
            if (ModelState.IsValid)
            {
                var movie = model.Movie;
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                foreach (var genreId in SelectedGenres)
                    _context.MovieGenres.Add(new MovieGenre { MovieId = movie.MovieId, GenreId = genreId });

                foreach (var roomId in SelectedRoomIds)
                    _context.Showtimes.Add(new Showtime { MovieId = movie.MovieId, RoomId = roomId, StartTime = model.StartTime ?? DateTime.Now });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Phim đã được thêm thành công!";
                return RedirectToAction(nameof(Index));
            }

            SetCommonData();
            model.Genres = _context.Genres.ToList();
            model.Theaters = _context.Theaters.ToList();
            model.SelectedGenres = SelectedGenres;
            model.SelectedTheaterId = new List<int>();
            return View("~/Views/Manager/Create.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.MovieGenres)
                .Include(m => m.Showtimes)
                .ThenInclude(s => s.Room)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
            {
                TempData["Error"] = "Phim không tồn tại!";
                return RedirectToAction("Index");
            }

            var model = new MovieFormViewModel
            {
                Movie = movie,
                Genres = _context.Genres.ToList(),
                Theaters = _context.Theaters.ToList(),
                SelectedGenres = movie.MovieGenres.Select(mg => mg.GenreId).ToList(),
                SelectedTheaterId = movie.Showtimes.Select(s => s.RoomId).Distinct().ToList(),
                StartTime = movie.Showtimes.FirstOrDefault()?.StartTime
            };

            SetCommonData();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(MovieFormViewModel model, List<int> SelectedGenres, List<int> SelectedRoomIds)
        {
            if (!ModelState.IsValid)
            {
                SetCommonData();
                model.Genres = _context.Genres.ToList();
                model.Theaters = _context.Theaters.ToList();
                model.SelectedGenres = SelectedGenres;
                model.SelectedTheaterId = SelectedRoomIds;
                return View(model);
            }

            var movieInDb = await _context.Movies
                .Include(m => m.MovieGenres)
                .Include(m => m.Showtimes)
                .FirstOrDefaultAsync(m => m.MovieId == model.Movie.MovieId);

            if (movieInDb == null)
            {
                TempData["Error"] = "Phim không tồn tại!";
                return RedirectToAction("Index");
            }

            movieInDb.Title = model.Movie.Title;
            movieInDb.Description = model.Movie.Description;
            movieInDb.Duration = model.Movie.Duration;
            movieInDb.PosterUrl = model.Movie.PosterUrl;
            movieInDb.VideoUrl = model.Movie.VideoUrl;
            movieInDb.ReleaseDate = model.Movie.ReleaseDate;

            _context.MovieGenres.RemoveRange(movieInDb.MovieGenres);
            foreach (var genreId in SelectedGenres)
                _context.MovieGenres.Add(new MovieGenre { MovieId = movieInDb.MovieId, GenreId = genreId });

            foreach (var showtime in movieInDb.Showtimes)
                showtime.StartTime = model.StartTime ?? showtime.StartTime;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật phim thành công!";
            }
            catch
            {
                TempData["Error"] = "Có lỗi khi cập nhật phim!";
                return View(model);
            }

            return RedirectToAction("Index");
        }
    }
}
