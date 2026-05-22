using ClintCardShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class CollectionService(IDbContextFactory<CollectionDbContext> factory)
{
    // ── Add / Remove / Move ───────────────────────────────────────────────

    public async Task<CardInstance> AddInstanceAsync(
        string cardId, string variant, string locationId,
        string? purchaseId, IEnumerable<string> listIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instance = new CardInstance
        {
            CardId     = cardId,
            Variant    = variant,
            LocationId = locationId,
            PurchaseId = purchaseId,
            AddedAt    = DateTime.UtcNow
        };
        db.CardInstances.Add(instance);
        foreach (var listId in listIds)
            db.ListMembers.Add(new ListMember { ListId = listId, InstanceId = instance.Id });
        await db.SaveChangesAsync();
        return instance;
    }

    public async Task RemoveInstanceAsync(string instanceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instance = await db.CardInstances.FindAsync(instanceId);
        if (instance is null) return;
        db.ListMembers.RemoveRange(db.ListMembers.Where(m => m.InstanceId == instanceId));
        db.CardInstances.Remove(instance);
        await db.SaveChangesAsync();
    }

    public async Task MoveInstanceAsync(string instanceId, string newLocationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instance = await db.CardInstances.FindAsync(instanceId);
        if (instance is null) return;
        instance.LocationId = newLocationId;
        await db.SaveChangesAsync();
    }

    public async Task MoveToDefaultLocationAsync(string locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instances = await db.CardInstances.Where(i => i.LocationId == locationId).ToListAsync();
        foreach (var i in instances)
            i.LocationId = CollectionDbContext.DefaultLocationId;
        await db.SaveChangesAsync();
    }

    // ── Queries ───────────────────────────────────────────────────────────

    public async Task<List<CardInstance>> GetByCardAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.AddedAt)
            .ToListAsync();
    }

    public async Task<List<CardInstance>> GetByLocationAsync(string locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .Where(i => i.LocationId == locationId)
            .OrderBy(i => i.CardId)
            .ToListAsync();
    }

    // cardId → total count across all locations
    public async Task<Dictionary<string, int>> GetCountsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .GroupBy(i => i.CardId)
            .Select(g => new { CardId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CardId, x => x.Count);
    }

    // "cardId:variant" → count across all locations (for set page chips)
    public async Task<Dictionary<string, int>> GetAllVariantCountsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.CardInstances
            .GroupBy(i => new { i.CardId, i.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }

    // variant → count for a single card
    public async Task<Dictionary<string, int>> GetVariantCountsForCardAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .Where(i => i.CardId == cardId)
            .GroupBy(i => i.Variant)
            .Select(g => new { Variant = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Variant, x => x.Count);
    }

    // "cardId:variant" → count for a specific location
    public async Task<Dictionary<string, int>> GetVariantCountsByLocationAsync(string locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.CardInstances
            .Where(i => i.LocationId == locationId)
            .GroupBy(i => new { i.CardId, i.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }

    // locationId → count for all locations (for location list view)
    public async Task<Dictionary<string, int>> GetCountsPerLocationAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .GroupBy(i => i.LocationId)
            .Select(g => new { LocationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LocationId, x => x.Count);
    }
}
