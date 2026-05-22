using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Data;

public class CollectionDbContext(DbContextOptions<CollectionDbContext> options) : DbContext(options)
{
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<Location> Locations => Set<Location>();

    public DbSet<CardList> CardLists => Set<CardList>();
    public DbSet<ListEntry> ListEntries => Set<ListEntry>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseCard> PurchaseCards => Set<PurchaseCard>();

    public DbSet<ApiCacheEntry> ApiCache => Set<ApiCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApiCacheEntry>()
            .HasKey(e => e.Url);

        modelBuilder.Entity<CollectionEntry>()
            .HasKey(e => new { e.CardId, e.LocationId, e.Variant });

        modelBuilder.Entity<Location>()
            .HasKey(l => l.Id);

        modelBuilder.Entity<CardList>()
            .HasKey(l => l.Id);
        modelBuilder.Entity<CardList>()
            .HasIndex(l => l.ShareToken)
            .IsUnique();

        modelBuilder.Entity<ListEntry>()
            .HasKey(e => new { e.ListId, e.CardId, e.LocationId, e.Variant });

        modelBuilder.Entity<PurchaseCard>()
            .HasKey(e => new { e.PurchaseId, e.CardId, e.LocationId, e.Variant });
    }
}

public class CollectionEntry
{
    public string CardId { get; set; } = "";
    public int LocationId { get; set; } = 0;    // 0 = Unassigned
    public string Variant { get; set; } = "Normal";
    public int Count { get; set; }
}

public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Box";
}

public class CardList
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? ShareToken { get; set; }
    public string Type { get; set; } = "Standard";  // "Standard" | "Wishlist"
}

public class ListEntry
{
    public int ListId { get; set; }
    public string CardId { get; set; } = "";
    public int LocationId { get; set; }
    public string Variant { get; set; } = "Normal";
    public int Count { get; set; }
}

public class PurchaseCard
{
    public int PurchaseId { get; set; }
    public string CardId { get; set; } = "";
    public int LocationId { get; set; } = 0;   // which copy/location this came from; 0 = unspecified
    public string Variant { get; set; } = "Normal";
    public int Quantity { get; set; } = 1;
}

public class ApiCacheEntry
{
    public string Url { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CachedAt { get; set; }
}

public class Purchase
{
    public int Id { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.Today;
    public string Source { get; set; } = "";          // e.g. "eBay", "Walgreens"
    public string Description { get; set; } = "";
    public decimal TotalCost { get; set; }
    public int Quantity { get; set; } = 1;
    public string Type { get; set; } = "Other";       // Lot, Pack, Single, Bundle, Other
    public string? ReceiptUrl { get; set; }
    public string? Notes { get; set; }
}
