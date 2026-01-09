using System.ComponentModel.DataAnnotations;

namespace bothomthit.Models
{
    // --- DTO CHO TRIP (CHUYẾN ĐI) ---
    public class CreateTripRequest
    {
        [Required] public string Title { get; set; } = string.Empty;
        [Required] public DateTime StartDate { get; set; }
        [Required] public DateTime EndDate { get; set; }
        public string? CoverImageUrl { get; set; }
    }

    public class UpdateTripRequest
    {
        public string? Title { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? CoverImageUrl { get; set; }
    }

    // --- DTO CHO ITINERARY (LỊCH TRÌNH CHI TIẾT) ---

    // 1. Dùng để Tạo mới (POST)
    public class CreateItineraryItemRequest
    {
        [Required] public string Type { get; set; } = "Activity"; // Flight, Hotel, Activity...
        [Required] public string Title { get; set; } = string.Empty;
        public string? BookingReference { get; set; } // Mã đặt chỗ
        public string? LocationName { get; set; }
        [Required] public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentUrl { get; set; } // Link ảnh vé/PDF
    }

    // 2. Dùng để Cập nhật (PUT) - Cho phép sửa từng phần
    public class UpdateItineraryItemRequest
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? BookingReference { get; set; }
        public string? LocationName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentUrl { get; set; }
    }

    // 3. Dùng để Trả về (GET) - Nếu muốn ẩn bớt thông tin hoặc format lại
    public class ItineraryItemDto
    {
        public int ItemId { get; set; }
        public int TripId { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string? BookingReference { get; set; }
        public string? LocationName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
        public string? AttachmentUrl { get; set; }
    }
}