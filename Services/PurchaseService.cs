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

    public async Task<Purchase> AddAsync(Purchase purchase)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();
        return purchase;
    }

    public async Task UpdateAsync(Purchase purchase)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Purchases.Update(purchase);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await factory.CreateDbContextAsync();
        // Unlink all instances from this purchase
        var instances = await db.CardInstances.Where(i => i.PurchaseId == id).ToListAsync();
        foreach (var i in instances) i.PurchaseId = null;
        var p = await db.Purchases.FindAsync(id);
        if (p is not null) db.Purchases.Remove(p);
        await db.SaveChangesAsync();
    }

    // ── Queries ───────────────────────────────────────────────────────────

    public async Task<List<CardInstance>> GetInstancesByPurchaseAsync(string purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .Where(i => i.PurchaseId == purchaseId)
            .OrderBy(i => i.CardId)
            .ToListAsync();
    }

    public async Task<int> GetLinkedCountAsync(string purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances.CountAsync(i => i.PurchaseId == purchaseId);
    }

    // purchaseId → linked instance count
    public async Task<Dictionary<string, int>> GetLinkedCountsPerPurchaseAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.CardInstances
            .Where(i => i.PurchaseId != null)
            .GroupBy(i => i.PurchaseId!)
            .Select(g => new { PurchaseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PurchaseId, x => x.Count);
    }

    // instances linked to a card across all purchases (for modal Purchases tab)
    public async Task<List<(Purchase P, CardInstance Instance)>> GetInstancesByCardAsync(string cardId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instances = await db.CardInstances
            .Where(i => i.CardId == cardId && i.PurchaseId != null)
            .ToListAsync();
        if (instances.Count == 0) return new();
        var purchaseIds = instances.Select(i => i.PurchaseId!).Distinct().ToList();
        var purchases   = await db.Purchases.Where(p => purchaseIds.Contains(p.Id)).ToListAsync();
        var lookup      = purchases.ToDictionary(p => p.Id);
        return instances
            .Where(i => lookup.ContainsKey(i.PurchaseId!))
            .Select(i => (P: lookup[i.PurchaseId!], Instance: i))
            .OrderByDescending(x => x.P.PurchasedAt)
            .ToList();
    }

    public async Task UnlinkInstanceAsync(string instanceId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instance = await db.CardInstances.FindAsync(instanceId);
        if (instance is null) return;
        instance.PurchaseId = null;
        await db.SaveChangesAsync();
    }

    public async Task LinkInstanceAsync(string instanceId, string purchaseId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instance = await db.CardInstances.FindAsync(instanceId);
        if (instance is null) return;
        instance.PurchaseId = purchaseId;
        await db.SaveChangesAsync();
    }
}
