using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClintCardShop.Controllers;

[Route("partials")]
[Authorize]
public class DetailPartialsController(
    TcgDexService tcgDex,
    CollectionService collection,
    LocationService locationSvc,
    CardListService listSvc,
    PurchaseService purchaseSvc) : Controller
{
    // ── List: share ──────────────────────────────────────────────────────

    [HttpPost("list/{id}/share")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListShare(string id)
    {
        var lists = await listSvc.GetAllAsync();
        var list  = lists.FirstOrDefault(l => l.Id == id);
        if (list is null) return NotFound();

        if (list.ShareToken is null)
            await listSvc.GenerateShareTokenAsync(id);

        lists = await listSvc.GetAllAsync();
        list  = lists.First(l => l.Id == id);

        var baseUrl  = $"{Request.Scheme}://{Request.Host}";
        var shareUrl = $"{baseUrl}/list/share/{list.ShareToken}";
        return PartialView("~/Views/Partials/_ListSharePanel.cshtml",
            new ListSharePanelViewModel(id, shareUrl));
    }

    [HttpPost("list/{id}/revoke-share")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListRevokeShare(string id)
    {
        await listSvc.RevokeShareTokenAsync(id);
        return Content("", "text/html");
    }

    // ── List: remove card ────────────────────────────────────────────────

    [HttpPost("list/{id}/remove-card")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ListRemoveCard(string id, string cardId)
    {
        var instances = await listSvc.GetInstancesByListAsync(id);
        var toRemove  = instances.FirstOrDefault(i => i.CardId == cardId);
        if (toRemove is not null)
            await listSvc.RemoveInstanceFromListAsync(id, toRemove.Id);
        return await ListCardsResult(id);
    }

    [HttpGet("list/{id}/cards")]
    public async Task<IActionResult> ListCards(string id)
        => await ListCardsResult(id);

    private async Task<IActionResult> ListCardsResult(string id)
    {
        var lists   = await listSvc.GetAllAsync();
        var list    = lists.FirstOrDefault(l => l.Id == id);
        if (list is null) return NotFound();

        var isWishlist = list.Type == "Wishlist";
        Dictionary<string, int> listCounts;

        if (isWishlist)
        {
            var entries = await listSvc.GetWishlistEntriesAsync(id);
            listCounts  = entries.ToDictionary(e => $"{e.CardId}:{e.Variant}", e => e.WantedCount);
        }
        else
        {
            var instances = await listSvc.GetInstancesByListAsync(id);
            listCounts    = instances
                .GroupBy(i => $"{i.CardId}:{i.Variant}")
                .ToDictionary(g => g.Key, g => g.Count());
        }

        var cardBriefs = await CardLoader.LoadFromVariantCountsAsync(listCounts, tcgDex);

        Dictionary<string, int> displayCounts = listCounts;
        if (isWishlist)
        {
            var colCounts = await collection.GetAllVariantCountsAsync();
            displayCounts = listCounts
                .Where(kv => kv.Value > 0)
                .ToDictionary(
                    kv => kv.Key,
                    kv => Math.Max(0, kv.Value - colCounts.GetValueOrDefault(kv.Key, 0)));
        }

        return PartialView("~/Views/Partials/_DetailCardGrid.cshtml",
            new DetailCardGridViewModel(
                cardBriefs.Select(x => x.Card).ToList(),
                displayCounts,
                $"/partials/list/{id}/remove-card",
                null));
    }

    // ── Purchase: unlink card ────────────────────────────────────────────

    [HttpPost("purchase/{id}/unlink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseUnlink(string id, string cardId)
    {
        var instances = await purchaseSvc.GetInstancesByPurchaseAsync(id);
        var toRemove  = instances.FirstOrDefault(i => i.CardId == cardId);
        if (toRemove is not null)
            await purchaseSvc.UnlinkInstanceAsync(toRemove.Id);
        return await PurchasePanelResult(id);
    }

    [HttpGet("purchase/{id}/cards")]
    public async Task<IActionResult> PurchaseCards(string id)
        => await PurchasePanelResult(id);

    private async Task<IActionResult> PurchasePanelResult(string id)
    {
        var purchases     = await purchaseSvc.GetAllAsync();
        var purchase      = purchases.FirstOrDefault(p => p.Id == id);
        if (purchase is null) return NotFound();
        var instances     = await purchaseSvc.GetInstancesByPurchaseAsync(id);
        var variantCounts = instances
            .GroupBy(i => $"{i.CardId}:{i.Variant}")
            .ToDictionary(g => g.Key, g => g.Count());
        var cardBriefs    = await CardLoader.LoadFromVariantCountsAsync(variantCounts, tcgDex);
        return PartialView("~/Views/Partials/_PurchaseCardsPanel.cshtml",
            new PurchaseCardsPanelViewModel(purchase, cardBriefs, variantCounts));
    }

    // ── Purchase: edit ───────────────────────────────────────────────────

    [HttpGet("purchase/{id}/cancel")]
    public async Task<IActionResult> PurchaseCancelEdit(string id)
    {
        var purchases = await purchaseSvc.GetAllAsync();
        var purchase  = purchases.FirstOrDefault(p => p.Id == id);
        if (purchase is null) return NotFound();
        return PartialView("~/Views/Partials/_PurchaseInfo.cshtml", purchase);
    }

    [HttpGet("purchase/{id}/edit-form")]
    public async Task<IActionResult> PurchaseEditForm(string id)
    {
        var purchases = await purchaseSvc.GetAllAsync();
        var purchase  = purchases.FirstOrDefault(p => p.Id == id);
        if (purchase is null) return NotFound();
        return PartialView("~/Views/Partials/_PurchaseEditForm.cshtml", purchase);
    }

    [HttpPost("purchase/{id}/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseSave(string id, [FromForm] Purchase p)
    {
        p.Id = id;
        await purchaseSvc.UpdateAsync(p);
        return PartialView("~/Views/Partials/_PurchaseInfo.cshtml", p);
    }

}
