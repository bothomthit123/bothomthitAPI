using System;

namespace bothomthit.Models.DTOs
{
    // DTO này chứa mọi thứ Flutter cần để hiển thị chi tiết
    public class AdvertisementDetailDto
    {
        // Từ Advertisement
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? BannerImageUrl { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }

        // Từ Place (thông qua JOIN)
        public int PlaceId { get; set; }
        public string PlaceName { get; set; } = null!;
        public string? PlaceAddress { get; set; } // Giả sử Bảng Place có cột Address
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}