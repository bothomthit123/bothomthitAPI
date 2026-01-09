namespace bothomthit.Models
{
    public class Favorite
    {
        public int FavoriteId { get; set; }
        public int AccountId { get; set; }
        public int PlaceId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
