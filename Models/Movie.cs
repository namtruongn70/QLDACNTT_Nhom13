using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CinemaManagement.Models
{
    public class Movie
    {
        public int MovieId { get; set; }

        [Required]
        public string Title { get; set; }

        public string Description { get; set; }

        [Range(1, 500, ErrorMessage = "Thời lượng phải từ 1 đến 500 phút")]
        public int Duration { get; set; }

        [Url]
        public string PosterUrl { get; set; }

        public DateTime ReleaseDate { get; set; }

        public double Rating { get; set; } = 0;

        public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>(); // ✅ Khởi tạo mặc định

        [Url]
        public string VideoUrl { get; set; } // Link YouTube gốc

        // Quan hệ nhiều-nhiều với Genre
        public List<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();

        // Tự động chuyển đổi VideoUrl thành Embed Url
        public string EmbedVideoUrl
        {
            get
            {
                if (string.IsNullOrEmpty(VideoUrl)) return "";
                return VideoUrl.Replace("watch?v=", "embed/");
            }
        }
        public ICollection<MovieRating> MovieRatings { get; set; } = new List<MovieRating>();
    }
}