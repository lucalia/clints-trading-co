using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class ListDetailModel(
    CardListService listSvc,
    CollectionService collection,
    TcgDexService tcgDex) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public CardList? List { get; private set; }
    public List<(CardBrief Card, int Count)> CardBriefs { get; private set; } = new();
    public Dictionary<string, int> ListVariantCounts { get; private set; } = new();
    public Dictionary<string, int> CollectionVariantCounts { get; private set; } = new();
    public Dictionary<string, int> WishlistDisplayCounts { get; private set; } = new();
    public List<WishlistEntry> WishlistEntries { get; private set; } = new();
    public bool IsWishlist => List?.Type == "Wishlist";
    public string? FulfillMsg { get; private set; }

    public async Task OnGetAsync()
    {
        var listTask = listSvc.GetAllAsync();
        var colTask  = collection.GetAllVariantCountsAsync();
        await Task.WhenAll(listTask, colTask);

        List                   = listTask.Result.FirstOrDefault(l => l.Id == Id);
        CollectionVariantCounts = colTask.Result;

        if (List is null) return;

        if (IsWishlist)
        {
            WishlistEntries = await listSvc.GetWishlistEntriesAsync(Id);
            var removed = await listSvc.FulfillWishlistAsync(Id);
            if (removed.Count > 0)
            {
                WishlistEntries = await listSvc.GetWishlistEntriesAsync(Id);
                var variantCountsTemp = WishlistEntries.ToDictionary(
                    e => $"{e.CardId}:{e.Variant}", e => e.WantedCount);
                var tempBriefs = await CardLoader.LoadFromVariantCountsAsync(variantCountsTemp, tcgDex);
                var names = removed.Select(e =>
                {
                    var brief = tempBriefs.FirstOrDefault(b => b.Card.Id == e.CardId);
                    return $"{(brief != default ? brief.Card.Name : e.CardId)} ({e.Variant})";
                });
                FulfillMsg = $"Goal met — removed from wishlist: {string.Join("; ", names)}.";
            }

            ListVariantCounts = WishlistEntries.ToDictionary(
                e => $"{e.CardId}:{e.Variant}", e => e.WantedCount);
            CardBriefs = await CardLoader.LoadFromVariantCountsAsync(ListVariantCounts, tcgDex);
            WishlistDisplayCounts = WishlistEntries.ToDictionary(
                e => $"{e.CardId}:{e.Variant}",
                e => Math.Max(0, e.WantedCount - CollectionVariantCounts.GetValueOrDefault($"{e.CardId}:{e.Variant}", 0)));
        }
        else
        {
            var instances = await listSvc.GetInstancesByListAsync(Id);
            ListVariantCounts = instances
                .GroupBy(i => $"{i.CardId}:{i.Variant}")
                .ToDictionary(g => g.Key, g => g.Count());
            CardBriefs = await CardLoader.LoadFromVariantCountsAsync(ListVariantCounts, tcgDex);
        }
    }
}
