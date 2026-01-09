namespace bothomthit.Models.DTOs
{
    public class AdvertisementMarkerDto
    {
        public int AdId { get; set; }
        public int PlaceId { get; set; }
        public string Title { get; set; } = null!;
        public string? BannerImageUrl { get; set; }

        // Đây là 2 trường quan trọng mà Flutter cần
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Có thể thêm các trường khác nếu client cần
        // public string? Description { get; set; } 
    }
}