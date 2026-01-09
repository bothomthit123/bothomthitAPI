using System.ComponentModel.DataAnnotations;

namespace bothomthit.Models
{
    public class SecureDocument
    {
        [Key]
        public int DocId { get; set; }
        public int AccountId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string DocType { get; set; } = "Passport"; // Passport, Visa...
        public string ImageUrl { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public bool IsPinned { get; set; } = false;
    }
}