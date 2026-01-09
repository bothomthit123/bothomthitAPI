using bothomthit.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // --- CÁC BẢNG ---
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>(); 
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<ExternalPlaceMap> ExternalPlaceMaps => Set<ExternalPlaceMap>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<ItineraryItem> ItineraryItems => Set<ItineraryItem>();
    public DbSet<SecureDocument> SecureDocuments => Set<SecureDocument>();

   

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("app");

        // 1. Account
        b.Entity<Account>(e =>
        {
            e.ToTable("Account");
            e.HasKey(x => x.AccountId);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(300);
        });

        // 2. Supplier
        b.Entity<Supplier>(e =>
        {
            e.ToTable("Supplier");
            e.HasKey(x => x.SupplierId);
        });

        // 3. Place
        b.Entity<Place>(e =>
        {
            e.ToTable("Place");
            e.HasKey(x => x.PlaceId);
            e.HasCheckConstraint("CK_Place_Source",
                "(IsPartnerPlace = 1 AND SupplierId IS NOT NULL) OR (IsPartnerPlace = 0 AND SupplierId IS NULL)");
            e.HasIndex(x => new { x.Latitude, x.Longitude });
        });

         //4. Advertisement
        
        b.Entity<Advertisement>(e =>
        {
            e.ToTable("Advertisement");
            e.HasKey(x => x.AdId);
            e.HasIndex(x => new { x.PlaceId, x.StartUtc, x.EndUtc })
             .HasDatabaseName("IX_Ad_Place_Active")
             .HasFilter("IsDeleted = 0");
        });
        

        // 5. Favorite
        b.Entity<Favorite>(e =>
        {
            e.ToTable("Favorite");
            e.HasKey(x => x.FavoriteId);
            e.HasIndex(x => new { x.AccountId, x.PlaceId }).IsUnique();
        });

        // 6. Review
        b.Entity<Review>(e =>
        {
            e.ToTable("Review");
            e.HasKey(x => x.ReviewId);
            e.HasIndex(x => new { x.AccountId, x.PlaceId }).IsUnique()
             .HasFilter("IsDeleted = 0");
        });

        // 7. SearchHistory
        b.Entity<SearchHistory>(e =>
        {
            e.ToTable("SearchHistory");
            e.HasKey(x => x.HistoryId);
            e.HasIndex(x => new { x.AccountId, x.SearchDateUtc })
             .HasDatabaseName("IX_Search_Account");
        });

        // 8. Recommendation
        b.Entity<Recommendation>(e =>
        {
            e.ToTable("Recommendation");
            e.HasKey(x => x.RecommendationId);
            e.HasIndex(x => x.AccountId).HasDatabaseName("IX_Rec_Account");
        });

        // 9. ExternalPlaceMap
        b.Entity<ExternalPlaceMap>(e =>
        {
            e.ToTable("ExternalPlaceMap");
            e.HasKey(x => new { x.Provider, x.ProviderPlaceId });
        });

        // =================================================
        // CẤU HÌNH CHO CÁC BẢNG TRIP & SECURE DOC
        // =================================================

        // 10. Trip
        b.Entity<Trip>(e =>
        {
            e.ToTable("Trip");
            e.HasKey(x => x.TripId);
            e.Property(x => x.Title).HasMaxLength(200);
            // Liên kết Trip -> Account (AccountId)
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        // 11. ItineraryItem
        b.Entity<ItineraryItem>(e =>
        {
            e.ToTable("ItineraryItem");
            e.HasKey(x => x.ItemId);
            // Liên kết ItineraryItem -> Trip (1 Trip có nhiều Item)
            e.HasOne(x => x.Trip)
             .WithMany(t => t.Items)
             .HasForeignKey(x => x.TripId)
             .OnDelete(DeleteBehavior.Cascade); // Xóa Trip thì xóa luôn Item con
        });

        // 12. SecureDocument
        b.Entity<SecureDocument>(e =>
        {
            e.ToTable("SecureDocument");
            e.HasKey(x => x.DocId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}