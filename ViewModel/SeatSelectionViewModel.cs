using CinemaManagement.Models;
using System.Collections.Generic;

namespace CinemaManagement.ViewModels
{
    public class SeatSelectionViewModel
    {
        public Showtime Showtime { get; set; }
        public List<Seat> AvailableSeats { get; set; }
        public Room Room { get; set; }
        public int ShowtimeId => Showtime?.ShowtimeId ?? 0;
        public Movie Movie { get; set; }
        public decimal Price { get; set; } // Giá gốc
        public decimal BasePrice { get; set; }

        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStart { get; set; }
        public DateTime? DiscountEnd { get; set; }

        public int RoomId => Room?.RoomId ?? 0;
        public List<int> SelectedSeats { get; set; } = new List<int>();
        public List<int> PaidSeatIds { get; set; } = new List<int>();
        public List<int> ReservedSeatIds { get; set; } = new List<int>();
    }
}
