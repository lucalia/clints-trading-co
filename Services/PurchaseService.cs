using ClintCardShop.Data;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class PurchaseService(IDbContextFactory<CollectionDbContext> factory)
{
    public async Task<List<Purchase>> GetAllAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Purchases.OrderByDescending(p => p.PurchasedAt).ToListAsync();
    }

    public async Task AddAsync(Purchase purchase)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Purchase purchase)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Purchases.Update(purchase);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.PurchaseCards.RemoveRange(db.PurchaseCards.Where(c => c.PurchaseId == id));
        var p = await db.Purchases.FindAsync(id);
        if (p is not null) db.Purchases.Remove(p);
        await db.SaveChangesAsync();
    }

    // ── Card links ────────────────────────────────────────────────────────

    // Returns {cardId}:{variant} → quantity for all cards in a purchase
    public async Task<Dictionary<string, int>> GetVariantCountsByPurchaseAsync(int purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.PurchaseCards
            .Where(c => c.PurchaseId == purchaseId)
            .ToListAsync();
        return rows.ToDictionary(c => $"{c.CardId}:{c.Variant}", c => c.Quantity);
    }

    public async Task SetPurchaseCardAsync(int purchaseId, string cardId, int locationId, string variant, int quantity)
    {
        await using var db = await factory.CreateDbContextAsync();
        var entry = await db.PurchaseCards.FindAsync(purchaseId, cardId, locationId, variant);
        if (quantity <= 0)
        {
            if (entry is not null) db.PurchaseCards.Remove(entry);
        }
        else if (entry is null)
        {
            db.PurchaseCards.Add(new PurchaseCard { PurchaseId = purchaseId, CardId = cardId, LocationId = locationId, Variant = variant, Quantity = quantity });
        }
        else
        {
            entry.Quantity = quantity;
        }
        await db.SaveChangesAsync();
    }

    public async Task RemovePurchaseCardAsync(int purchaseId, string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.PurchaseCards.RemoveRange(db.PurchaseCards.Where(c => c.PurchaseId == purchaseId && c.CardId == cardId));
        await db.SaveChangesAsync();
    }

    // Returns all purchases that contain a given card (with location) for modal Purchases tab
    public async Task<List<(Purchase P, int LocationId, string Variant, int Quantity)>> GetPurchasesByCardAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var links = await db.PurchaseCards.Where(c => c.CardId == cardId).ToListAsync();
        if (links.Count == 0) return new();
        var purchaseIds = links.Select(l => l.PurchaseId).Distinct().ToList();
        var purchases   = await db.Purchases.Where(p => purchaseIds.Contains(p.Id)).ToListAsync();
        var lookup      = purchases.ToDictionary(p => p.Id);
        return links
            .Where(l => lookup.ContainsKey(l.PurchaseId))
            .Select(l => (P: lookup[l.PurchaseId], l.LocationId, l.Variant, l.Quantity))
            .OrderByDescending(x => x.P.PurchasedAt)
            .ToList();
    }

    // Returns total quantity linked to purchases per card (for consolidation badge)
    public async Task<Dictionary<string, int>> GetLinkedQuantityByCardAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PurchaseCards
            .GroupBy(c => c.CardId)
            .Select(g => new { CardId = g.Key, Total = g.Sum(c => c.Quantity) })
            .ToDictionaryAsync(x => x.CardId, x => x.Total);
    }

    public async Task<Dictionary<int, int>> GetCardCountsPerPurchaseAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PurchaseCards
            .GroupBy(c => c.PurchaseId)
            .Select(g => new { PurchaseId = g.Key, Count = g.Select(c => c.CardId).Distinct().Count() })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Count);
    }

    // Returns total linked quantity per purchase (sum of all PurchaseCard.Quantity rows)
    public async Task<Dictionary<int, int>> GetLinkedQuantityPerPurchaseAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.PurchaseCards
            .GroupBy(c => c.PurchaseId)
            .Select(g => new { PurchaseId = g.Key, Total = g.Sum(c => c.Quantity) })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Total);
    }
}
