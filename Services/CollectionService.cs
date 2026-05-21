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

    // Returns Dictionary keyed by "{cardId}:{variant}" → count for a specific location
    public async Task<Dictionary<string, int>> GetVariantCountsByLocationAsync(int locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.CollectionEntries
            .Where(e => e.LocationId == locationId)
            .GroupBy(e => new { e.CardId, e.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Sum(e => e.Count) })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }

    public async Task MoveCardCountAsync(string cardId, string variant, int fromLocationId, int toLocationId, int count)
    {
        if (fromLocationId == toLocationId || count <= 0) return;
        await using var db = await factory.CreateDbContextAsync();
        var src = await db.CollectionEntries.FindAsync(cardId, fromLocationId, variant);
        if (src is null || src.Count < count) return;
        src.Count -= count;
        if (src.Count == 0) db.CollectionEntries.Remove(src);
        var dest = await db.CollectionEntries.FindAsync(cardId, toLocationId, variant);
        if (dest is null)
            db.CollectionEntries.Add(new CollectionEntry { CardId = cardId, LocationId = toLocationId, Variant = variant, Count = count });
        else
            dest.Count += count;
        await db.SaveChangesAsync();
    }

    public async Task MoveCardsAsync(int fromLocationId, int toLocationId)
    {
        if (fromLocationId == toLocationId) return;
        await using var db = await factory.CreateDbContextAsync();
        var sources = await db.CollectionEntries
            .Where(e => e.LocationId == fromLocationId)
            .ToListAsync();
        foreach (var src in sources)
        {
            var dest = await db.CollectionEntries.FindAsync(src.CardId, toLocationId, src.Variant);
            if (dest is null)
                db.CollectionEntries.Add(new CollectionEntry { CardId = src.CardId, LocationId = toLocationId, Variant = src.Variant, Count = src.Count });
            else
                dest.Count += src.Count;
            db.CollectionEntries.Remove(src);
        }
        await db.SaveChangesAsync();
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
