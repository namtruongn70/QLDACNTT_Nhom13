using System;
using System.ComponentModel.DataAnnotations;

namespace CinemaManagement.Models
{
    public class ShowtimeViewModel
    {
        public int ShowtimeId { get; set; }

        public int MovieId { get; set; }

        [Display(Name = "Rạp")]
        [Required(ErrorMessage = "Phải chọn rạp")]
        public int TheaterId { get; set; }

        [Display(Name = "Phòng")]
        [Required(ErrorMessage = "Phải chọn phòng")]
        public int RoomId { get; set; }

        [Display(Name = "Thời gian bắt đầu")]
        [Required(ErrorMessage = "Phải nhập thời gian bắt đầu")]
        public DateTime StartTime { get; set; }

        [Display(Name = "Giá tiền")]
        [Range(1, 99999999, ErrorMessage = "Giá phải từ 1 đến 99999999")]
        public decimal Price { get; set; }
        [Display(Name = "Giảm Giá")]
        public decimal? DiscountPercent { get; set; }
        public DateTime? DiscountStart { get; set; }
        public DateTime? DiscountEnd { get; set; }
    }
}
