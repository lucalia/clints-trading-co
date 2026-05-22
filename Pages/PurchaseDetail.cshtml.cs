using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class PurchaseDetailModel(
    PurchaseService purchaseSvc,
    TcgDexService tcgDex) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public Purchase? Purchase { get; private set; }
    public List<(CardBrief Card, int Count)> CardBriefs { get; private set; } = new();
    public Dictionary<string, int> LinkedVariantCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var purchases = await purchaseSvc.GetAllAsync();
        Purchase = purchases.FirstOrDefault(p => p.Id == Id);
        if (Purchase is null) return;

        var instances = await purchaseSvc.GetInstancesByPurchaseAsync(Id);
        LinkedVariantCounts = instances
            .GroupBy(i => $"{i.CardId}:{i.Variant}")
            .ToDictionary(g => g.Key, g => g.Count());
        CardBriefs = await CardLoader.LoadFromVariantCountsAsync(LinkedVariantCounts, tcgDex);
    }
}
