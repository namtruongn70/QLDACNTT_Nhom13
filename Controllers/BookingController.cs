using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinemaManagement.Data;
using CinemaManagement.Models;
using CinemaManagement.Services;
using CinemaManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using QRCoder;
using System.Drawing.Imaging;
using System.Drawing;

namespace CinemaManagement.Controllers
{
    public class BookingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly MomoService _momoService;
        private readonly EmailService _emailService;
        private readonly ILogger<BookingController> _logger;
        private const int ReservationMinutes = 15;

        public BookingController(AppDbContext context, MomoService momoService, ILogger<BookingController> logger, EmailService emailService)
        {
            _context = context;
            _momoService = momoService;
            _logger = logger;
            _emailService = emailService;
        }

        // Step 1: Hiển thị danh sách suất chiếu theo phim
        public async Task<IActionResult> Index(int? movieId)
        {
            if (!movieId.HasValue)
            {
                _logger.LogWarning("Index: movieId null");
                return NotFound("Không tìm thấy phim!");
            }

            var showtimes = await _context.Showtimes
                .Where(s => s.MovieId == movieId)
                .Include(s => s.Movie)
                    .ThenInclude(m => m.MovieGenres)
                        .ThenInclude(mg => mg.Genre)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Theater)
                .ToListAsync();

            if (!showtimes.Any())
            {
                _logger.LogWarning($"Index: Không tìm thấy suất chiếu cho movieId={movieId}");
                return NotFound("Không tìm thấy suất chiếu nào.");
            }

            var vm = new BookingViewModel
            {
                Movie = showtimes[0].Movie,
                Showtimes = showtimes
            };
            return View(vm);
        }

        // Step 2: Hiển thị danh sách ghế
        [Authorize]
        public async Task<IActionResult> SelectSeats(int showtimeId)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.Room)
                    .ThenInclude(r => r.Theater)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null || showtime.Room == null || showtime.Movie == null)
            {
                _logger.LogWarning($"SelectSeats: ShowtimeId={showtimeId} hoặc Room/Movie null");
                return NotFound("Lịch chiếu, phòng hoặc phim không tồn tại!");
            }

            var seats = await _context.Seats
            .Where(s => s.RoomId == showtime.RoomId)
            .Select(s => new Seat
            {
                SeatId = s.SeatId,
                SeatNumber = s.SeatNumber,
                IsBooked = false
            })
            .ToListAsync();

            var paidSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId && t.IsPaid)
                .Select(t => t.SeatId)
                .ToListAsync();

            var cutoff = System.DateTime.UtcNow.AddMinutes(-ReservationMinutes);
            var reservedSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId && !t.IsPaid && t.BookingTime >= cutoff)
                .Select(t => t.SeatId)
                .ToListAsync();

            HttpContext.Session.SetInt32("ShowtimeId", showtimeId);

            decimal basePrice = showtime.Price * (showtime.Room.PriceMultiplier ?? 1);
            decimal finalPrice = basePrice;

            if (showtime.DiscountPercent.HasValue && showtime.DiscountStart.HasValue && showtime.DiscountEnd.HasValue)
            {
                var now = System.DateTime.UtcNow;
                if (now >= showtime.DiscountStart.Value && now <= showtime.DiscountEnd.Value)
                {
                    finalPrice = basePrice * (1 - ((decimal)showtime.DiscountPercent.Value / 100));
                }
            }

            var vm = new SeatSelectionViewModel
            {
                Showtime = showtime,
                Room = showtime.Room,
                Movie = showtime.Movie,
                AvailableSeats = seats,
                Price = finalPrice,
                BasePrice = basePrice,
                DiscountPercent = showtime.DiscountPercent,
                DiscountStart = showtime.DiscountStart,
                DiscountEnd = showtime.DiscountEnd,
                PaidSeatIds = paidSeatIds,
                ReservedSeatIds = reservedSeatIds
            };

            return View(vm);
        }

        // Step 3: Xác nhận đặt vé
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ConfirmBooking([FromBody] BookingRequest req)
        {
            if (req?.SeatIds == null || !req.SeatIds.Any())
                return Json(new { success = false, error = "Chưa chọn ghế!" });

            var seatIds = req.SeatIds.Distinct().ToList();

            var showtime = await _context.Showtimes
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.ShowtimeId == req.ShowtimeId);

            if (showtime == null)
                return Json(new { success = false, error = "Suất chiếu không hợp lệ!" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Json(new { success = false, error = "Vui lòng đăng nhập để đặt vé." });

            var cutoff = DateTime.UtcNow.AddMinutes(-ReservationMinutes);

            var takenSeatIds = await _context.Tickets
                .Where(t => t.ShowtimeId == req.ShowtimeId &&
                           (t.IsPaid || (!t.IsPaid && t.BookingTime >= cutoff)))
                .Select(t => t.SeatId)
                .ToListAsync();

            if (seatIds.Any(id => takenSeatIds.Contains(id)))
                return Json(new { success = false, error = "Một số ghế đã được đặt hoặc đang trong quá trình thanh toán. Vui lòng chọn ghế khác." });

            var orderId = $"ORDER_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString().Substring(0, 6)}";

            decimal basePrice = showtime.Price * (showtime.Room.PriceMultiplier ?? 1);
            decimal pricePerSeat = basePrice;

            if (showtime.DiscountPercent.HasValue && showtime.DiscountStart.HasValue && showtime.DiscountEnd.HasValue)
            {
                var now = DateTime.UtcNow;
                if (now >= showtime.DiscountStart.Value && now <= showtime.DiscountEnd.Value)
                {
                    pricePerSeat = basePrice * (1 - ((decimal)showtime.DiscountPercent.Value / 100));
                }
            }

            var bookingTime = DateTime.UtcNow;

            foreach (var seatId in seatIds)
            {
                var seat = await _context.Seats.FindAsync(seatId);
                if (seat != null) seat.IsBooked = true;

                var ticket = new Ticket
                {
                    UserId = userId,
                    ShowtimeId = showtime.ShowtimeId,
                    SeatId = seatId,
                    Price = pricePerSeat,
                    IsPaid = false,
                    BookingTime = bookingTime,
                    OrderId = orderId,
                    PaymentMethod = "Pending" // <-- Mặc định
                };
                _context.Tickets.Add(ticket);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu vé đặt vào cơ sở dữ liệu");
                return Json(new { success = false, error = "Lỗi hệ thống, vui lòng thử lại." });
            }

            long totalAmount = (long)(pricePerSeat * seatIds.Count);
            HttpContext.Session.SetString("OrderId", orderId);
            HttpContext.Session.SetString("TotalAmount", totalAmount.ToString());

            return Json(new { success = true, orderId, totalAmount });
        }

        // Step 4: Lấy QR code Momo
        [HttpPost]
        public async Task<IActionResult> GetMomoQRCode([FromBody] PaymentRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.OrderId) || req.Amount <= 0)
                return BadRequest(new { success = false, error = "Dữ liệu không hợp lệ." });

            string qrUrl;
            try { qrUrl = await _momoService.GeneratePaymentQRCode(req.Amount, req.OrderId); }
            catch
            {
                return StatusCode(500, new { success = false, error = "Lỗi khi tạo QR code thanh toán." });
            }

            // Update PaymentMethod là Pending
            var tickets = await _context.Tickets.Where(t => t.OrderId == req.OrderId).ToListAsync();
            foreach (var t in tickets)
                t.PaymentMethod = "Pending";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, qrUrl });
        }

        // Step 5: Notify Momo
        [HttpPost("/api/momo/notify")]
        public async Task<IActionResult> MomoNotifyUrl()
        {
            string body;
            using (var reader = new StreamReader(Request.Body))
                body = await reader.ReadToEndAsync();

            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
            if (data == null || !data.ContainsKey("orderId") || !data.ContainsKey("resultCode"))
                return BadRequest();

            if (data["resultCode"] == "0")
            {
                var tickets = await _context.Tickets.Where(t => t.OrderId == data["orderId"] && !t.IsPaid).ToListAsync();
                foreach (var t in tickets)
                {
                    t.IsPaid = true;
                    t.PaymentMethod = "Momo"; // từ Pending → Momo
                    var seat = await _context.Seats.FindAsync(t.SeatId);
                    if (seat != null) seat.IsBooked = true; // khóa vĩnh viễn
                }
                await _context.SaveChangesAsync();
            }


            return Ok();
        }

        // Step 6: Payment success
        public async Task<IActionResult> PaymentSuccess(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return View("Error");

            var tickets = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Seat)
                    .ThenInclude(s => s.Room)
                        .ThenInclude(r => r.Theater)
                .Include(t => t.Showtime)
                    .ThenInclude(st => st.Movie)
                .Where(t => t.OrderId == orderId && t.IsPaid)
                .ToListAsync();

            if (!tickets.Any()) return View("Error");

            string qrCodeBase64 = GenerateQRCode($"OrderId={orderId}");

            var vm = new PaymentSuccessViewModel
            {
                Tickets = tickets,
                QrCodeBase64 = qrCodeBase64
            };

            try
            {
                var userEmail = tickets.First().User?.Email;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    string htmlContent = "<h2>🎫 Xác nhận vé xem phim</h2>";
                    foreach (var t in tickets)
                    {
                        htmlContent += $"<p><strong>Phim:</strong> {t.Showtime.Movie.Title}</p>";
                        htmlContent += $"<p><strong>Rạp:</strong> {t.Seat.Room.Theater.Name}</p>";
                        htmlContent += $"<p><strong>Phòng:</strong> {t.Seat.Room.Name}</p>";
                        htmlContent += $"<p><strong>Ngày chiếu:</strong> {t.Showtime.StartTime:dd/MM/yyyy}</p>";
                        htmlContent += $"<p><strong>Giờ chiếu:</strong> {t.Showtime.StartTime:HH:mm}</p><hr/>";
                        htmlContent += $"<p><strong>Ghế:</strong> {t.Seat.SeatNumber}</p>";
                    }
                    _ = _emailService.SendTicketEmailAsync(userEmail, "Xác nhận vé xem phim", htmlContent, $"OrderId={orderId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email xác nhận vé");
            }

            return View(vm);
        }

        //Step 7: Khóa ghế đã thanh toán
        [HttpGet]
        public async Task<IActionResult> GetPaidSeats(int showtimeId)
        {
            if (showtimeId <= 0) return BadRequest();

            var paidTickets = await _context.Tickets
                .Where(t => t.ShowtimeId == showtimeId && t.IsPaid)
                .Select(t => new { t.SeatId, t.PaymentMethod })
                .ToListAsync();

            return Json(new { paidSeats = paidTickets });
        }

        private string GenerateQRCode(string content)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCode(qrData);
            using var ms = new MemoryStream();
            using var bitmap = qrCode.GetGraphic(20);
            {
                bitmap.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        [Authorize(Roles = "Employee")]
        [HttpPost]
        public async Task<IActionResult> CashPayment([FromBody] CashPaymentRequest req)
        {
            if (string.IsNullOrEmpty(req.OrderId))
                return Json(new { success = false, error = "OrderId không hợp lệ" });

            var tickets = await _context.Tickets
                .Where(t => t.OrderId == req.OrderId && !t.IsPaid)
                .Include(t => t.Seat)
                .ToListAsync();

            if (!tickets.Any())
                return Json(new { success = false, error = "Không tìm thấy vé để thanh toán" });

            foreach (var t in tickets)
            {
                t.IsPaid = true;
                t.PaymentMethod = "Cash";
                if (t.Seat != null) t.Seat.IsBooked = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public class CashPaymentRequest
        {
            public string OrderId { get; set; }
        }
    }

    public class BookingRequest
    {
        public int ShowtimeId { get; set; }
        public List<int> SeatIds { get; set; }
        public string OrderId { get; set; }
    }

    public class PaymentRequest
    {
        public string OrderId { get; set; }
        public long Amount { get; set; }
    }
}
