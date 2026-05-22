using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class SetModel(TcgDexService tcgDex, CollectionService collection) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string SetId { get; set; } = "";

    public SetResponse? Set { get; private set; }
    public Dictionary<string, int> VariantCounts { get; private set; } = new();
    public Dictionary<string, int> Counts { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var setTask     = tcgDex.GetSetAsync(SetId);
            var variantTask = collection.GetAllVariantCountsAsync();
            await Task.WhenAll(setTask, variantTask);
            Set           = setTask.Result;
            VariantCounts = variantTask.Result;
            Counts        = VariantCounts
                .GroupBy(kv => kv.Key.Split(':')[0])
                .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
