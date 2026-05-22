using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class IndexModel(TcgDexService tcgDex) : PageModel
{
    public List<(string Serie, List<SetBrief> Sets)> Groups { get; private set; } = new();
    public int TotalSets { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var raw = await tcgDex.GetSetsAsync();
            Groups = SetGrouping.Build(raw);
            TotalSets = Groups.Sum(g => g.Sets.Count);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
