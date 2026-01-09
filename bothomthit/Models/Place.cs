using NetTopologySuite.Geometries;
using System.Text.Json.Serialization; // Thêm cái này

namespace bothomthit.Models
{
    public class Place
    {
        public int PlaceId { get; set; }
        public string Name { get; set; } = null!;
        public string? Address { get; set; }
        public string? Description { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public decimal? Rating { get; set; }
        public string? OpeningHours { get; set; }
        public string? ClosingHours { get; set; }
        public string? Category { get; set; }

        public bool IsPartnerPlace { get; set; } = false;
        public int? SupplierId { get; set; }
        public bool IsDeleted { get; set; } = false;

        // === [THÊM MỚI] Navigation Properties (Để sửa lỗi CS1061) ===

        // 1. Liên kết 1-N với bảng Reviews
        [JsonIgnore]
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        // 2. Liên kết 1-N với bảng ExternalPlaceMap (Một địa điểm có thể có mapping ngoài)
        [JsonIgnore]
        public virtual ICollection<ExternalPlaceMap> ExternalMaps { get; set; } = new List<ExternalPlaceMap>();
    }
}