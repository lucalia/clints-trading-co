using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Data;

public class CollectionDbContext(DbContextOptions<CollectionDbContext> options) : DbContext(options)
{
    public static readonly string DefaultLocationId = Guid.Empty.ToString();

    public DbSet<CardInstance>   CardInstances   => Set<CardInstance>();
    public DbSet<Location>       Locations       => Set<Location>();
    public DbSet<Purchase>       Purchases       => Set<Purchase>();
    public DbSet<CardList>       CardLists       => Set<CardList>();
    public DbSet<ListMember>     ListMembers     => Set<ListMember>();
    public DbSet<WishlistEntry>  WishlistEntries => Set<WishlistEntry>();
    public DbSet<ApiCacheEntry>  ApiCache        => Set<ApiCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardInstance>().HasKey(e => e.Id);
        modelBuilder.Entity<Location>().HasKey(e => e.Id);
        modelBuilder.Entity<Purchase>().HasKey(e => e.Id);
        modelBuilder.Entity<CardList>().HasKey(e => e.Id);
        modelBuilder.Entity<CardList>().HasIndex(e => e.ShareToken).IsUnique();
        modelBuilder.Entity<ListMember>().HasKey(e => new { e.ListId, e.InstanceId });
        modelBuilder.Entity<WishlistEntry>().HasKey(e => e.Id);
        modelBuilder.Entity<ApiCacheEntry>().HasKey(e => e.Url);
    }
}

public class CardInstance
{
    public string  Id         { get; set; } = Guid.NewGuid().ToString();
    public string  CardId     { get; set; } = "";
    public string  Variant    { get; set; } = "Normal";
    public string  LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public string? PurchaseId { get; set; }
    public DateTime AddedAt   { get; set; } = DateTime.UtcNow;
}

public class Location
{
    public string Id   { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Box";
}

public class Purchase
{
    public string   Id          { get; set; } = Guid.NewGuid().ToString();
    public DateTime PurchasedAt { get; set; } = DateTime.Today;
    public string   Source      { get; set; } = "";
    public string   Description { get; set; } = "";
    public decimal  TotalCost   { get; set; }
    public int      Quantity    { get; set; } = 1;
    public string   Type        { get; set; } = "Other";
    public string?  ReceiptUrl  { get; set; }
    public string?  Notes       { get; set; }
}

public class CardList
{
    public string  Id         { get; set; } = Guid.NewGuid().ToString();
    public string  Name       { get; set; } = "";
    public string? ShareToken { get; set; }
    public string  Type       { get; set; } = "Standard";
}

public class ListMember
{
    public string ListId     { get; set; } = "";
    public string InstanceId { get; set; } = "";
}

public class WishlistEntry
{
    public string Id          { get; set; } = Guid.NewGuid().ToString();
    public string ListId      { get; set; } = "";
    public string CardId      { get; set; } = "";
    public string Variant     { get; set; } = "Normal";
    public int    WantedCount { get; set; } = 1;
}

public class ApiCacheEntry
{
    public string   Url      { get; set; } = "";
    public string   Body     { get; set; } = "";
    public DateTime CachedAt { get; set; }
}
