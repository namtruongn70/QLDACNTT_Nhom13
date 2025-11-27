using System.ComponentModel.DataAnnotations;

namespace CinemaManagement.Models
{
    public class Theater
    {
        public int TheaterId { get; set; }

        [Required(ErrorMessage = "Tên rạp là bắt buộc")]
        public string Name { get; set; }

        public string Location { get; set; }
       
        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
