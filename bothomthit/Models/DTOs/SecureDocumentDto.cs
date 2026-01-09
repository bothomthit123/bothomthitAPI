namespace bothomthit.Models
{
    public class CreateSecureDocRequest
    {
        public string Title { get; set; } = string.Empty;
        public string DocType { get; set; } = "Passport"; // Passport, Visa, ID, Insurance
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public bool IsPinned { get; set; } = false;
    }
}