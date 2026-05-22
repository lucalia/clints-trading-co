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

    public async Task<Location> AddAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var location = new Location { Name = name.Trim() };
        db.Locations.Add(location);
        await db.SaveChangesAsync();
        return location;
    }

    public async Task UpdateAsync(string id, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var location = await db.Locations.FindAsync(id);
        if (location is null) return;
        location.Name = name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await collection.MoveToDefaultLocationAsync(id);
        await using var db = await factory.CreateDbContextAsync();
        var location = await db.Locations.FindAsync(id);
        if (location is not null)
        {
            db.Locations.Remove(location);
            await db.SaveChangesAsync();
        }
    }
}
