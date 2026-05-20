using ClintCardShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class CollectionService(IDbContextFactory<CollectionDbContext> factory)
{
    public async Task<Dictionary<string, int>> GetAllVariantCountsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.CollectionEntries
            .GroupBy(e => new { e.CardId, e.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Sum(e => e.Count) })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetCardVariantTotalsAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CollectionEntries
            .Where(e => e.CardId == cardId)
            .GroupBy(e => e.Variant)
            .Select(g => new { Variant = g.Key, Count = g.Sum(e => e.Count) })
            .ToDictionaryAsync(x => x.Variant, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetAllCountsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CollectionEntries
            .GroupBy(e => e.CardId)
            .Select(g => new { CardId = g.Key, Total = g.Sum(e => e.Count) })
            .ToDictionaryAsync(x => x.CardId, x => x.Total);
    }

    // Returns Dictionary keyed by "{locationId}:{variant}" → count
    public async Task<Dictionary<string, int>> GetCardBreakdownAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = await db.CollectionEntries.Where(e => e.CardId == cardId).ToListAsync();
        return entries.ToDictionary(e => $"{e.LocationId}:{e.Variant}", e => e.Count);
    }

    public async Task<int> AdjustCountAsync(string cardId, int locationId, string variant, int delta)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.CollectionEntries.FindAsync(cardId, locationId, variant);
        int locCount;

        if (entry is null)
        {
            locCount = Math.Max(0, delta);
            if (locCount > 0)
                db.CollectionEntries.Add(new CollectionEntry { CardId = cardId, LocationId = locationId, Variant = variant, Count = locCount });
        }
        else
        {
            locCount = Math.Max(0, entry.Count + delta);
            if (locCount == 0)
                db.CollectionEntries.Remove(entry);
            else
                entry.Count = locCount;
        }

        await db.SaveChangesAsync();

        return await db.CollectionEntries
            .Where(e => e.CardId == cardId)
            .SumAsync(e => e.Count);
    }

    public async Task<Dictionary<string, int>> GetCardsByLocationAsync(int locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CollectionEntries
            .Where(e => e.LocationId == locationId)
            .GroupBy(e => e.CardId)
            .Select(g => new { CardId = g.Key, Count = g.Sum(e => e.Count) })
            .ToDictionaryAsync(x => x.CardId, x => x.Count);
    }

    public async Task MoveToUnassignedAsync(int locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = await db.CollectionEntries.Where(e => e.LocationId == locationId).ToListAsync();
        foreach (var entry in entries)
        {
            var unassigned = await db.CollectionEntries.FindAsync(entry.CardId, 0, entry.Variant);
            if (unassigned is null)
                db.CollectionEntries.Add(new CollectionEntry { CardId = entry.CardId, LocationId = 0, Variant = entry.Variant, Count = entry.Count });
            else
                unassigned.Count += entry.Count;
            db.CollectionEntries.Remove(entry);
        }
        await db.SaveChangesAsync();
    }
}
