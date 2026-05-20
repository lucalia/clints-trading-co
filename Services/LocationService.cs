using ClintCardShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class LocationService(IDbContextFactory<CollectionDbContext> factory, CollectionService collection)
{
    public async Task<List<Location>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Locations.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<Dictionary<int, int>> GetCardCountsPerLocationAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CollectionEntries
            .GroupBy(e => e.LocationId)
            .Select(g => new { LocationId = g.Key, Total = g.Sum(e => e.Count) })
            .ToDictionaryAsync(x => x.LocationId, x => x.Total);
    }

    public async Task<Location> AddAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var location = new Location { Name = name.Trim(), Type = "" };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location;
    }

    public async Task UpdateAsync(int id, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var location = await db.Locations.FindAsync(id);
        if (location is null) return;
        location.Name = name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await collection.MoveToUnassignedAsync(id);
        await using var db = await factory.CreateDbContextAsync();
        var location = await db.Locations.FindAsync(id);
        if (location is not null)
        {
            db.Locations.Remove(location);
            await db.SaveChangesAsync();
        }
    }
}
