using ClintCardShop.Services;

namespace ClintCardShop.Models;

public static class CardLoader
{
    public static async Task<List<(CardBrief Card, int Count)>> LoadFromVariantCountsAsync(
        Dictionary<string, int> variantCounts,
        TcgDexService tcgDex)
    {
        var cardIds = variantCounts.Keys
            .Select(k => k.Split(':')[0])
            .Distinct()
            .ToHashSet();

        if (cardIds.Count == 0) return new();

        var setIds = cardIds
            .Select(id => { var i = id.LastIndexOf('-'); return i > 0 ? id[..i] : id; })
            .Distinct()
            .ToArray();

        var sets   = await Task.WhenAll(setIds.Select(s => tcgDex.GetSetAsync(s)));
        var lookup = sets
            .Where(s => s is not null)
            .SelectMany(s => s!.Cards)
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First());

        return cardIds.Select(id =>
        {
            var idx  = id.LastIndexOf('-');
            var fb   = new CardBrief { Id = id, Name = id, LocalId = idx > 0 ? id[(idx + 1)..] : id };
            var card = lookup.GetValueOrDefault(id) ?? fb;
            var count = variantCounts.Where(kv => kv.Key.StartsWith(id + ":")).Sum(kv => kv.Value);
            return (Card: card, Count: count);
        })
        .OrderBy(x => x.Card.Id)
        .ToList();
    }
}
