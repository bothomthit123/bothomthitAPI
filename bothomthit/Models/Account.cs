namespace bothomthit.Models
{
    public class Account
    {
        public int AccountId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "User"; // User|Supplier|Admin
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // FK optional -> Supplier
        public int? SupplierId { get; set; }
        public bool IsLocked { get; set; } = false;
    }
}
