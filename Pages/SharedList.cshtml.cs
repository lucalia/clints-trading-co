using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[AllowAnonymous]
public class SharedListModel(
    CardListService listSvc,
    TcgDexService tcgDex) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = "";

    public CardList? List { get; private set; }
    public List<(CardBrief Card, int Count)> CardBriefs { get; private set; } = new();
    public Dictionary<string, int> VariantCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        List = await listSvc.GetByShareTokenAsync(Token);
        if (List is null) return;

        if (List.Type == "Wishlist")
        {
            var entries = await listSvc.GetWishlistEntriesAsync(List.Id);
            VariantCounts = entries.ToDictionary(e => $"{e.CardId}:{e.Variant}", e => e.WantedCount);
        }
        else
        {
            var instances = await listSvc.GetInstancesByListAsync(List.Id);
            VariantCounts = instances
                .GroupBy(i => $"{i.CardId}:{i.Variant}")
                .ToDictionary(g => g.Key, g => g.Count());
        }
        CardBriefs = await CardLoader.LoadFromVariantCountsAsync(VariantCounts, tcgDex);
    }
}
