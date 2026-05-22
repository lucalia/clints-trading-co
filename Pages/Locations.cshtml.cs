using ClintCardShop.Data;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class LocationsModel(LocationService locationSvc, CollectionService collection) : PageModel
{
    public List<Location> Locations { get; private set; } = new();
    public Dictionary<string, int> CardCounts { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var locsTask   = locationSvc.GetAllAsync();
        var countsTask = collection.GetCountsPerLocationAsync();
        await Task.WhenAll(locsTask, countsTask);
        Locations  = locsTask.Result;
        CardCounts = countsTask.Result;
    }
}
