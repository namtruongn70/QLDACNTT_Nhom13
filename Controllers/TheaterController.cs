using CinemaManagement.Data;
using CinemaManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CinemaManagement.ViewModel;

using System.Linq;
using CinemaManagement.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CinemaManagement.Controllers
{
    [Authorize(Roles = SD.Role_Admin)]
    public class TheaterController : Controller
    {
        private readonly AppDbContext _context;

        public TheaterController(AppDbContext context)
        {
            _context = context;
        }

        // 📄 Danh sách tất cả rạp
        public IActionResult Index()
        {
            var theaters = _context.Theaters
                .Include(t => t.Rooms)
                .ToList();

            return View(theaters);
        }

        // 👁️ Xem chi tiết rạp
        public IActionResult Details(int id)
        {
            var theater = _context.Theaters
                .Include(t => t.Rooms) // ← thêm Include để lấy danh sách phòng
                .FirstOrDefault(t => t.TheaterId == id);

            if (theater == null) return NotFound();
            return View(theater);
        }

        // ➕ Form thêm rạp
        public IActionResult Create()
        {
            return View(new TheaterFormViewModel());
        }

        [HttpPost]
        public IActionResult Create(TheaterFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState không hợp lệ.");
                return View(model);
            }

            try
            {
                Console.WriteLine("👉 Bắt đầu tạo Theater...");
                _context.Theaters.Add(model.Theater);
                _context.SaveChanges();

                int theaterId = model.Theater.TheaterId;
                Console.WriteLine($"✅ Theater đã tạo với ID: {theaterId}");

                // 🟥 Hủy bỏ phần tạo phòng và ghế mặc định

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi khi tạo rạp: " + ex.Message);
                Console.WriteLine("🔍 StackTrace: " + ex.StackTrace);
                ModelState.AddModelError("", "Lỗi khi tạo rạp: " + ex.Message);
                return View(model);
            }
        }

        // ✏️ Form sửa rạp
        public async Task<IActionResult> Edit(int id)
        {
            var theater = await _context.Theaters
                .Include(t => t.Rooms)
                .FirstOrDefaultAsync(t => t.TheaterId == id);

            if (theater == null)
            {
                return NotFound();
            }

            return View(theater);
        }


        // 💾 Xử lý sửa rạp
        [HttpPost]
        public IActionResult Edit(int id, Theater updated)
        {
            if (id != updated.TheaterId) return NotFound();
            if (!ModelState.IsValid) return View(updated);

            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == id);
            if (theater == null) return NotFound();

            theater.Name = updated.Name;
            theater.Location = updated.Location;

            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        // 🗑️ Xác nhận xóa
        public IActionResult Delete(int id)
        {
            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == id);
            if (theater == null) return NotFound();

            return View(theater);
        }

        [HttpPost, ActionName("Delete")]
        public IActionResult DeleteConfirmed(int id)
        {
            var theater = _context.Theaters
                .Include(t => t.Rooms)
                .ThenInclude(r => r.Showtimes)
                .FirstOrDefault(t => t.TheaterId == id);

            if (theater == null)
                return NotFound();

            try
            {
                // Kiểm tra trước khi xóa
                bool hasShowtimes = theater.Rooms.Any(r => r.Showtimes.Any());
                if (hasShowtimes)
                {
                    TempData["ErrorMessage"] = "❌ Không thể xóa rạp vì có suất chiếu liên kết với phòng.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Xóa ghế và phòng trước
                foreach (var room in theater.Rooms)
                {
                    var seats = _context.Seats.Where(s => s.RoomId == room.RoomId);
                    _context.Seats.RemoveRange(seats);
                    _context.Rooms.Remove(room);
                }

                _context.Theaters.Remove(theater);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "✅ Xóa rạp thành công.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "❌ Lỗi khi xóa rạp: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        //Khu Vực Tạo Phòng

        // ➕ Form thêm phòng chiếu cho rạp (GET)
        [HttpGet]
        public IActionResult CreateRoom(int theaterId)
        {
            var theater = _context.Theaters.FirstOrDefault(t => t.TheaterId == theaterId);
            if (theater == null)
                return NotFound();

            var model = new RoomFormViewModel
            {
                TheaterId = theaterId
            };

            ViewBag.TheaterName = theater.Name;
            return View(model);
        }

        // 💾 Xử lý thêm phòng chiếu (POST)
        [HttpPost]
        public IActionResult CreateRoom(RoomFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState không hợp lệ.");
                return View(model);
            }

            try
            {
                // Debug: In ra các giá trị từ model trước khi thêm vào DB
                Console.WriteLine($"TheaterId: {model.TheaterId}");
                Console.WriteLine($"RoomName: {model.RoomName}");
                Console.WriteLine($"SeatCount: {model.SeatCount}");
                Console.WriteLine($"SeatCount: {model.PriceMultiplier}");
                Console.WriteLine($"SeatCount: {model.Description}");

                var room = new Room
                {
                    TheaterId = model.TheaterId,
                    Name = model.RoomName,
                    PriceMultiplier = model.PriceMultiplier ?? 1.0m,
                    Description = model.Description          
                };

                // Debug: Kiểm tra Room trước khi thêm
                Console.WriteLine($"Tạo phòng mới với TheaterId: {room.TheaterId}, Name: {room.Name}");

                // Thêm phòng vào DB
                _context.Rooms.Add(room);
                Console.WriteLine($"Đã thêm phòng vào DbContext với RoomId: {room.RoomId}");
                _context.SaveChanges();  // Lưu vào cơ sở dữ liệu
                Console.WriteLine("Phòng đã được lưu thành công!");

                // Debug: Kiểm tra số ghế
                for (int i = 1; i <= model.SeatCount; i++)
                {
                    var seat = new Seat
                    {
                        RoomId = room.RoomId,  // Liên kết với phòng chiếu vừa tạo
                        SeatNumber = i,
                        IsBooked = false
                    };
                    _context.Seats.Add(seat);
                    Console.WriteLine($"Thêm ghế số: {seat.SeatNumber} vào phòng {room.Name}");
                }

                // Lưu ghế vào cơ sở dữ liệu
                _context.SaveChanges();
                Console.WriteLine("Ghế đã được lưu thành công!");

                return RedirectToAction("Details", new { id = model.TheaterId }); // Quay lại trang chi tiết rạp
            }
            catch (Exception ex)
            {
                // Debug: Ghi lại lỗi chi tiết
                Console.WriteLine($"❌ Lỗi khi tạo phòng: {ex.Message}");
                ModelState.AddModelError("", "Lỗi khi tạo phòng: " + ex.Message);
                return View(model);
            }
        }


        // ✏️ Form sửa phòng chiếu
        public IActionResult EditRoom(int id)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);
            if (room == null) return NotFound();

            var model = new RoomFormViewModel
            {
                TheaterId = room.TheaterId,
                RoomName = room.Name,
                SeatCount = _context.Seats.Count(s => s.RoomId == id)
            };

            return View(model);
        }

        // 💾 Xử lý sửa phòng chiếu
        [HttpPost]
        public IActionResult EditRoom(int id, RoomFormViewModel model)
        {
            if (id != model.TheaterId) return NotFound();
            if (!ModelState.IsValid) return View(model);

            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);
            if (room == null) return NotFound();

            room.Name = model.RoomName;

            // Xử lý cập nhật ghế (nếu cần thay đổi số ghế)
            var existingSeats = _context.Seats.Where(s => s.RoomId == id).ToList();
            int currentSeatCount = existingSeats.Count();
            int seatDifference = model.SeatCount - currentSeatCount;

            // Nếu số ghế tăng, thêm ghế mới
            if (seatDifference > 0)
            {
                for (int i = currentSeatCount + 1; i <= model.SeatCount; i++)
                {
                    var newSeat = new Seat
                    {
                        RoomId = room.RoomId,
                        SeatNumber = i,
                        IsBooked = false
                    };
                    _context.Seats.Add(newSeat);
                }
            }
            // Nếu số ghế giảm, xóa ghế thừa
            else if (seatDifference < 0)
            {
                var seatsToRemove = existingSeats.Take(-seatDifference).ToList();
                _context.Seats.RemoveRange(seatsToRemove);
            }

            _context.SaveChanges();
            return RedirectToAction("Details", new { id = room.TheaterId });
        }

        [HttpPost]
        public IActionResult DeleteRoom(int id)
        {
            var room = _context.Rooms.FirstOrDefault(r => r.RoomId == id);
            if (room == null) return NotFound();

            bool hasShowtimes = _context.Showtimes.Any(s => s.RoomId == id);
            if (hasShowtimes)
            {
                return BadRequest("Không thể xóa phòng vì đang có suất chiếu liên quan.");
            }

            // Xóa ghế
            var seats = _context.Seats.Where(s => s.RoomId == id);
            _context.Seats.RemoveRange(seats);

            _context.Rooms.Remove(room);
            _context.SaveChanges();

            return Ok();
        }


        [HttpGet]
        public IActionResult GetRoomsByTheater(int theaterId)
        {
            var rooms = _context.Rooms
                .Where(r => r.TheaterId == theaterId)
                .Select(r => new {
                    r.RoomId,
                    r.Name
                }).ToList();

            return Json(rooms);
        }

    }
}
