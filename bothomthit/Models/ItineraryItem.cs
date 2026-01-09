using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace bothomthit.Models
{
    public class ItineraryItem
    {
        [Key]
        public int ItemId { get; set; }
        public int TripId { get; set; }

        // Loại: 'Flight', 'Hotel', 'Train', 'Bus', 'Activity'
        public string Type { get; set; } = "Activity";

        public string Title { get; set; } = string.Empty;
        public string? BookingReference { get; set; } // Mã vé
        public string? LocationName { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentUrl { get; set; }

        [JsonIgnore]
        public Trip? Trip { get; set; }
    }
}