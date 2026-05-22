using ClintCardShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class CardListService(IDbContextFactory<CollectionDbContext> factory)
{
    public async Task<List<CardList>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardLists.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task AddAsync(string name, string type = "Standard")
    {
        await using var db = await factory.CreateDbContextAsync();
        db.CardLists.Add(new CardList { Name = name.Trim(), Type = type });
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(string id, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(id);
        if (list is null) return;
        list.Name = name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.ListMembers.RemoveRange(db.ListMembers.Where(m => m.ListId == id));
        db.WishlistEntries.RemoveRange(db.WishlistEntries.Where(e => e.ListId == id));
        var list = await db.CardLists.FindAsync(id);
        if (list is not null) db.CardLists.Remove(list);
        await db.SaveChangesAsync();
    }

    // ── Standard list (instance-based) ───────────────────────────────────

    public async Task<List<CardInstance>> GetInstancesByListAsync(string listId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instanceIds = await db.ListMembers
            .Where(m => m.ListId == listId)
            .Select(m => m.InstanceId)
            .ToListAsync();
        return await db.CardInstances
            .Where(i => instanceIds.Contains(i.Id))
            .OrderBy(i => i.CardId)
            .ToListAsync();
    }

    public async Task AddInstanceToListAsync(string listId, string instanceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.ListMembers.AnyAsync(m => m.ListId == listId && m.InstanceId == instanceId);
        if (!exists)
        {
            db.ListMembers.Add(new ListMember { ListId = listId, InstanceId = instanceId });
            await db.SaveChangesAsync();
        }
    }

    public async Task RemoveInstanceFromListAsync(string listId, string instanceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var member = await db.ListMembers.FindAsync(listId, instanceId);
        if (member is not null)
        {
            db.ListMembers.Remove(member);
            await db.SaveChangesAsync();
        }
    }

    // listId → count of unique cardIds on that list
    public async Task<Dictionary<string, int>> GetUniqueCardCountsPerListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        // For standard lists: count unique cardIds through ListMembers → CardInstances
        var standardCounts = await db.ListMembers
            .Join(db.CardInstances, m => m.InstanceId, i => i.Id, (m, i) => new { m.ListId, i.CardId })
            .GroupBy(x => x.ListId)
            .Select(g => new { ListId = g.Key, Count = g.Select(x => x.CardId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ListId, x => x.Count);

        // For wishlists: count unique cardIds in WishlistEntries
        var wishlistCounts = await db.WishlistEntries
            .GroupBy(e => e.ListId)
            .Select(g => new { ListId = g.Key, Count = g.Select(e => e.CardId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ListId, x => x.Count);

        foreach (var kv in wishlistCounts)
            standardCounts[kv.Key] = kv.Value;

        return standardCounts;
    }

    // Lists that contain a specific instance (for modal display)
    public async Task<List<CardList>> GetListsForInstanceAsync(string instanceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var listIds = await db.ListMembers
            .Where(m => m.InstanceId == instanceId)
            .Select(m => m.ListId)
            .ToListAsync();
        return await db.CardLists.Where(l => listIds.Contains(l.Id)).ToListAsync();
    }

    // ── Wishlist (type-based) ─────────────────────────────────────────────

    public async Task<List<WishlistEntry>> GetWishlistEntriesAsync(string listId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.WishlistEntries
            .Where(e => e.ListId == listId)
            .OrderBy(e => e.CardId)
            .ToListAsync();
    }

    public async Task SetWishlistEntryAsync(string listId, string cardId, string variant, int wantedCount)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.WishlistEntries
            .FirstOrDefaultAsync(e => e.ListId == listId && e.CardId == cardId && e.Variant == variant);
        if (wantedCount <= 0)
        {
            if (entry is not null) db.WishlistEntries.Remove(entry);
        }
        else if (entry is null)
        {
            db.WishlistEntries.Add(new WishlistEntry
                { ListId = listId, CardId = cardId, Variant = variant, WantedCount = wantedCount });
        }
        else
        {
            entry.WantedCount = wantedCount;
        }
        await db.SaveChangesAsync();
    }

    public async Task RemoveWishlistEntryAsync(string listId, string cardId, string variant)
        => await SetWishlistEntryAsync(listId, cardId, variant, 0);

    // Remove fulfilled wishlist entries (owned >= wanted). Returns removed entries for display.
    public async Task<List<(string CardId, string Variant, int Wanted)>> FulfillWishlistAsync(string listId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(listId);
        if (list?.Type != "Wishlist") return new();

        var entries = await db.WishlistEntries.Where(e => e.ListId == listId).ToListAsync();
        if (entries.Count == 0) return new();

        var cardIds = entries.Select(e => e.CardId).Distinct().ToList();
        var owned = await db.CardInstances
            .Where(i => cardIds.Contains(i.CardId))
            .GroupBy(i => new { i.CardId, i.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Count() })
            .ToListAsync();
        var lookup = owned.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);

        var removed = new List<(string CardId, string Variant, int Wanted)>();
        foreach (var entry in entries)
        {
            if (lookup.GetValueOrDefault($"{entry.CardId}:{entry.Variant}") >= entry.WantedCount)
            {
                db.WishlistEntries.Remove(entry);
                removed.Add((entry.CardId, entry.Variant, entry.WantedCount));
            }
        }
        if (removed.Count > 0) await db.SaveChangesAsync();
        return removed;
    }

    // ── Sharing ───────────────────────────────────────────────────────────

    public async Task<string> GenerateShareTokenAsync(string id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(id);
        if (list is null) return "";
        list.ShareToken = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        return list.ShareToken;
    }

    public async Task RevokeShareTokenAsync(string id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(id);
        if (list is null) return;
        list.ShareToken = null;
        await db.SaveChangesAsync();
    }

    public async Task<CardList?> GetByShareTokenAsync(string token)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardLists.FirstOrDefaultAsync(l => l.ShareToken == token);
    }
}
