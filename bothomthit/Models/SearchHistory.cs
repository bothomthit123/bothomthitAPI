namespace bothomthit.Models
{
    public class SearchHistory
    {
        public int HistoryId { get; set; }
        public int AccountId { get; set; }
        public string Keyword { get; set; } = null!;
        public DateTime SearchDateUtc { get; set; } = DateTime.UtcNow;
    }
}
