namespace bothomthit.Models
{
    public class Recommendation
    {
        public int RecommendationId { get; set; }
        public int AccountId { get; set; }
        public int PlaceId { get; set; }
        public double Score { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
