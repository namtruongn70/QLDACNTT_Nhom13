using CinemaManagement.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CinemaManagement.ViewModel
{
    public class MovieFormViewModel
    {
        public Movie Movie { get; set; }

        public decimal Price { get; set; }
        public List<Genre> Genres { get; set; } = new();
        public List<Theater> Theaters { get; set; } = new();
        public List<Room> Rooms { get; set; } = new();
        public List<int> SelectedGenres { get; set; } = new();
        public List<int> SelectedTheaterId { get; set; } = new();
        public List<int> SelectedRoomId { get; set; } = new();

        public DateTime? StartTime { get; set; }

    }

}
