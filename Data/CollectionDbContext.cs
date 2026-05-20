using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Data;

public class CollectionDbContext(DbContextOptions<CollectionDbContext> options) : DbContext(options)
{
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CollectionEntry>()
            .HasKey(e => new { e.CardId, e.LocationId, e.Variant });

        modelBuilder.Entity<Location>()
            .HasKey(l => l.Id);
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
