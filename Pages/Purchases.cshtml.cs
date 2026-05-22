using ClintCardShop.Data;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClintCardShop.Pages;

[Authorize]
public class PurchasesModel(PurchaseService purchaseSvc) : PageModel
{
    public List<Purchase> Purchases { get; private set; } = new();
    public Dictionary<string, int> LinkedQty { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var pTask = purchaseSvc.GetAllAsync();
        var qTask = purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        await Task.WhenAll(pTask, qTask);
        Purchases = pTask.Result;
        LinkedQty = qTask.Result;
    }
}
