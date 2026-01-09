namespace bothomthit.Models
{
    public class Advertisement
    {
        public int AdId { get; set; }
        public int PlaceId { get; set; }
        public int SupplierId { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? BannerImageUrl { get; set; } 
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}
