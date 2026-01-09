namespace bothomthit.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int AccountId { get; set; }
        public int PlaceId { get; set; }

        public decimal Rating { get; set; } // 0..5
        public string? Comment { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
    }
}
