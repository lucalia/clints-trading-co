using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class LocationDetailModel(
    LocationService locationSvc,
    CollectionService collection,
    TcgDexService tcgDex) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public string? LocationName { get; private set; }
    public bool IsNotFound { get; private set; }
    public List<(CardBrief Card, int Count)> CardBriefs { get; private set; } = new();
    public Dictionary<string, int> VariantCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        if (Id == CollectionDbContext.DefaultLocationId || string.IsNullOrEmpty(Id))
        {
            Id = CollectionDbContext.DefaultLocationId;
            LocationName = "Collection";
        }
        else
        {
            var locs = await locationSvc.GetAllAsync();
            var loc  = locs.FirstOrDefault(l => l.Id == Id);
            if (loc is null) { IsNotFound = true; return; }
            LocationName = loc.Name;
        }

        VariantCounts = await collection.GetVariantCountsByLocationAsync(Id);
        CardBriefs    = await CardLoader.LoadFromVariantCountsAsync(VariantCounts, tcgDex);
    }
}
