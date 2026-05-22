using ClintCardShop.Data;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class ListsModel(CardListService listSvc) : PageModel
{
    public List<CardList> Lists { get; private set; } = new();
    public Dictionary<string, int> ListCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var listsTask  = listSvc.GetAllAsync();
        var countsTask = listSvc.GetUniqueCardCountsPerListAsync();
        await Task.WhenAll(listsTask, countsTask);
        Lists      = listsTask.Result;
        ListCounts = countsTask.Result;
    }
}
