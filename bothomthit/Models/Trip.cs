using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bothomthit.Models
{
    public class Trip
    {
        [Key]
        public int TripId { get; set; }
        public int AccountId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Quan hệ 1-N
        public ICollection<ItineraryItem> Items { get; set; } = new List<ItineraryItem>();
    }
}