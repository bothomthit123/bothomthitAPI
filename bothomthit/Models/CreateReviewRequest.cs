namespace bothomthit.Models
{
    public class CreateReviewRequest
    {
        // Thông tin Review
        public decimal Rating { get; set; }
        public string? Comment { get; set; }

        // Thông tin định danh địa điểm (Provider)
       
        public string Provider { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;

        // Thông tin chi tiết địa điểm (để tạo mới nếu cần)
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Category { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
// Model này dùng để hứng dữ liệu đánh giá để hiển thị chi tiết đánh giá cho địa điểm