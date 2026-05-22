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
    public async Task<IActionResult> ListRemoveCard(string id, [FromForm] string cardId)
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
    public async Task<IActionResult> PurchaseUnlink(string id, [FromForm] string cardId)
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

    // ── Add card flow ────────────────────────────────────────────────────

    [HttpGet("add-card/sets")]
    public async Task<IActionResult> AddCardSets(string context, string ctxId, string? q)
    {
        var allSets = await tcgDex.GetSetsAsync();
        var groups  = SetGrouping.Build(allSets);
        var sets    = groups.SelectMany(g => g.Sets).ToList();

        HashSet<string> ownedSetIds = new();
        if (context == "list")
        {
            var owned = await collection.GetCountsAsync();
            ownedSetIds = owned.Keys
                .Select(id => { var i = id.LastIndexOf('-'); return i > 0 ? id[..i] : id; })
                .ToHashSet();
        }

        if (!string.IsNullOrWhiteSpace(q))
            sets = sets.Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                   s.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        return PartialView("~/Views/Partials/_AddCardSets.cshtml",
            new AddCardSetsViewModel(context, ctxId, q ?? "", sets, ownedSetIds));
    }

    [HttpGet("add-card/cards")]
    public async Task<IActionResult> AddCardPick(string context, string ctxId, string setId, string? q, string? successMsg)
    {
        var setTask = tcgDex.GetSetAsync(setId);
        var colTask = collection.GetAllVariantCountsAsync();
        await Task.WhenAll(setTask, colTask);

        var cards = setTask.Result?.Cards ?? new();
        if (!string.IsNullOrWhiteSpace(q))
            cards = cards.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     c.LocalId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        return PartialView("~/Views/Partials/_AddCardPick.cshtml",
            new AddCardPickViewModel(context, ctxId, setId,
                setTask.Result?.Name ?? setId, cards, colTask.Result, successMsg));
    }

    [HttpGet("add-card/configure")]
    public async Task<IActionResult> AddCardConfigure(string context, string ctxId, string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx]    : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;

        var setTask    = tcgDex.GetSetAsync(setId);
        var detailTask = tcgDex.GetCardAsync(setId, localId);
        var locsTask   = locationSvc.GetAllAsync();
        await Task.WhenAll(setTask, detailTask, locsTask);

        var card     = setTask.Result?.Cards.FirstOrDefault(c => c.Id == cardId)
                       ?? new CardBrief { Id = cardId, Name = cardId, LocalId = localId };
        var detail   = detailTask.Result;
        var variants = PartialsController.AvailableVariants(detail);
        var locs     = locsTask.Result;

        bool isWishlist = false;
        int maxCount    = 999;

        if (context == "list")
        {
            var lists = await listSvc.GetAllAsync();
            var list  = lists.FirstOrDefault(l => l.Id == ctxId);
            isWishlist = list?.Type == "Wishlist";
            if (!isWishlist)
            {
                var colCounts = await collection.GetAllVariantCountsAsync();
                maxCount = Math.Max(1,
                    colCounts.Where(kv => kv.Key.StartsWith(cardId + ":")).Sum(kv => kv.Value));
            }
        }
        else if (context == "purchase")
        {
            var instances = await purchaseSvc.GetInstancesByPurchaseAsync(ctxId);
            var purchases = await purchaseSvc.GetAllAsync();
            var purchase  = purchases.FirstOrDefault(p => p.Id == ctxId);
            if (purchase is not null)
            {
                maxCount = Math.Max(0, purchase.Quantity - instances.Count);
            }
        }

        return PartialView("~/Views/Partials/_AddCardConfigure.cshtml",
            new AddCardConfigureViewModel(context, ctxId, card, detail,
                variants, locs, maxCount, isWishlist));
    }

    [HttpPost("add-card/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCardSubmit([FromForm] AddCardSubmitRequest req)
    {
        string? purchaseId = req.Context == "purchase" ? req.CtxId : null;
        var listIds = req.Context == "list" ? new[] { req.CtxId } : Array.Empty<string>();

        if (req.Context == "list")
        {
            var lists = await listSvc.GetAllAsync();
            var list  = lists.FirstOrDefault(l => l.Id == req.CtxId);
            if (list?.Type == "Wishlist")
            {
                await listSvc.SetWishlistEntryAsync(req.CtxId, req.CardId, req.Variant, req.Count);
                listIds = Array.Empty<string>();
                purchaseId = null;
                // Don't create instances for wishlist
            }
            else
            {
                for (int i = 0; i < req.Count; i++)
                    await collection.AddInstanceAsync(req.CardId, req.Variant, req.LocationId, purchaseId, listIds);
                listIds = Array.Empty<string>();
            }
        }
        else if (req.Context == "purchase")
        {
            for (int i = 0; i < req.Count; i++)
                await collection.AddInstanceAsync(req.CardId, req.Variant, req.LocationId, purchaseId, Array.Empty<string>());
        }

        var idx      = req.CardId.LastIndexOf('-');
        var setId    = idx > 0 ? req.CardId[..idx] : req.CardId;
        var set      = await tcgDex.GetSetAsync(setId);
        var cardName = set?.Cards.FirstOrDefault(c => c.Id == req.CardId)?.Name ?? req.CardId;
        var successMsg = $"{cardName} ({req.Variant}) added!";

        if (req.Context == "purchase")
        {
            // Retarget to #purchase-panel so linked count + reconcile status refresh too
            Response.Headers.Append("HX-Retarget", "#purchase-panel");
            Response.Headers.Append("HX-Reswap", "innerHTML");
            return await PurchasePanelResult(req.CtxId);
        }

        return await AddCardPick(req.Context, req.CtxId, setId, null, successMsg);
    }
}
