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

    public async Task UpdateAsync(int id, string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(id);
        if (list is null) return;
        list.Name = name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = db.ListEntries.Where(e => e.ListId == id);
        db.ListEntries.RemoveRange(entries);
        var list = await db.CardLists.FindAsync(id);
        if (list is not null) db.CardLists.Remove(list);
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<int, int>> GetUniqueCardCountsPerListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ListEntries
            .GroupBy(e => e.ListId)
            .Select(g => new { ListId = g.Key, Count = g.Select(e => e.CardId).Distinct().Count() })
            .ToDictionaryAsync(x => x.ListId, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetVariantCountsByListAsync(int listId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.ListEntries
            .Where(e => e.ListId == listId)
            .GroupBy(e => new { e.CardId, e.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Sum(e => e.Count) })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }

    public async Task SetEntryAsync(int listId, string cardId, int locationId, string variant, int count)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.ListEntries.FindAsync(listId, cardId, locationId, variant);
        if (count <= 0)
        {
            if (entry is not null) db.ListEntries.Remove(entry);
        }
        else if (entry is null)
        {
            db.ListEntries.Add(new ListEntry { ListId = listId, CardId = cardId, LocationId = locationId, Variant = variant, Count = count });
        }
        else
        {
            entry.Count = count;
        }
        await db.SaveChangesAsync();
    }

    public async Task RemoveCardAsync(int listId, string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entries = db.ListEntries.Where(e => e.ListId == listId && e.CardId == cardId);
        db.ListEntries.RemoveRange(entries);
        await db.SaveChangesAsync();
    }

    public async Task<string> GenerateShareTokenAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(id);
        if (list is null) return "";
        list.ShareToken = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        return list.ShareToken;
    }

    public async Task RevokeShareTokenAsync(int id)
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

    // For wishlists: remove only fully-satisfied entries (owned >= wanted).
    // Never mutates entry counts — remaining is computed dynamically in the UI.
    // Returns the removed entries so the UI can name them.
    public async Task<List<(string CardId, string Variant, int Wanted)>> FulfillWishlistAsync(int listId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FindAsync(listId);
        if (list?.Type != "Wishlist") return new();

        var entries = await db.ListEntries.Where(e => e.ListId == listId).ToListAsync();
        if (entries.Count == 0) return new();

        var cardIds = entries.Select(e => e.CardId).Distinct().ToList();
        var owned = await db.CollectionEntries
            .Where(e => cardIds.Contains(e.CardId))
            .GroupBy(e => new { e.CardId, e.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Total = g.Sum(e => e.Count) })
            .ToListAsync();
        var lookup = owned.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Total);

        var removed = new List<(string CardId, string Variant, int Wanted)>();
        foreach (var entry in entries)
        {
            if (lookup.GetValueOrDefault($"{entry.CardId}:{entry.Variant}") >= entry.Count)
            {
                db.ListEntries.Remove(entry);
                removed.Add((entry.CardId, entry.Variant, entry.Count));
            }
            // Partial coverage: leave entry untouched; UI shows max(0, wanted - owned)
        }

        if (removed.Count > 0) await db.SaveChangesAsync();
        return removed;
    }

    public async Task<Dictionary<int, Dictionary<string, int>>> GetListVariantCountsByCardAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.ListEntries
            .Where(e => e.CardId == cardId)
            .GroupBy(e => new { e.ListId, e.Variant })
            .Select(g => new { g.Key.ListId, g.Key.Variant, Count = g.Sum(e => e.Count) })
            .ToListAsync();
        return rows
            .GroupBy(x => x.ListId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Variant, x => x.Count));
    }

    public async Task<Dictionary<string, int>> GetVariantCountsByTokenAsync(string token)
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.CardLists.FirstOrDefaultAsync(l => l.ShareToken == token);
        if (list is null) return new();
        var rows = await db.ListEntries
            .Where(e => e.ListId == list.Id)
            .GroupBy(e => new { e.CardId, e.Variant })
            .Select(g => new { g.Key.CardId, g.Key.Variant, Count = g.Sum(e => e.Count) })
            .ToListAsync();
        return rows.ToDictionary(x => $"{x.CardId}:{x.Variant}", x => x.Count);
    }
}
