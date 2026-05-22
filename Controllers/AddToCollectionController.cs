using ClintCardShop.Controllers;
using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClintCardShop.Controllers;

[Route("partials/add-to-collection")]
[Authorize]
public class AddToCollectionController(
    TcgDexService tcgDex,
    CollectionService collection,
    LocationService locationSvc,
    CardListService listSvc,
    PurchaseService purchaseSvc) : Controller
{
    [HttpGet("sets")]
    public async Task<IActionResult> Sets(string? q)
    {
        var vm = await BuildSetsViewModel(q);
        return PartialView("~/Views/AddToCollection/_Sets.cshtml", vm);
    }

    [HttpGet("sets-results")]
    public async Task<IActionResult> SetsResults(string? q)
    {
        var vm = await BuildSetsViewModel(q);
        return PartialView("~/Views/AddToCollection/_SetResults.cshtml", vm);
    }

    [HttpGet("cards")]
    public async Task<IActionResult> Cards(string setId, string? q)
    {
        var vm = await BuildCardsViewModel(setId, q);
        return PartialView("~/Views/AddToCollection/_Cards.cshtml", vm);
    }

    [HttpGet("cards-results")]
    public async Task<IActionResult> CardsResults(string setId, string? q)
    {
        var vm = await BuildCardsViewModel(setId, q);
        return PartialView("~/Views/AddToCollection/_CardResults.cshtml", vm);
    }

    [HttpGet("configure")]
    public async Task<IActionResult> Configure(string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx]    : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;

        var setTask       = tcgDex.GetSetAsync(setId);
        var detailTask    = tcgDex.GetCardAsync(setId, localId);
        var locsTask      = locationSvc.GetAllAsync();
        var purchasesTask = purchaseSvc.GetAllAsync();
        var linkedTask    = purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        var listsTask     = listSvc.GetAllAsync();
        await Task.WhenAll(setTask, detailTask, locsTask, purchasesTask, linkedTask, listsTask);

        var card          = setTask.Result?.Cards.FirstOrDefault(c => c.Id == cardId)
                            ?? new CardBrief { Id = cardId, Name = cardId, LocalId = localId };
        var variants      = PartialsController.AvailableVariants(detailTask.Result);
        var standardLists = listsTask.Result.Where(l => l.Type != "Wishlist").ToList();

        return PartialView("~/Views/AddToCollection/_Configure.cshtml",
            new AddToCollectionConfigureViewModel(card, detailTask.Result, variants,
                locsTask.Result, purchasesTask.Result, linkedTask.Result, standardLists));
    }

    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromForm] AddToCollectionRequest req)
    {
        if (req.Quantity < 1) req.Quantity = 1;

        var locationId = string.IsNullOrEmpty(req.LocationId)
            ? CollectionDbContext.DefaultLocationId : req.LocationId;
        var purchaseId = string.IsNullOrEmpty(req.PurchaseId) ? null : req.PurchaseId;

        // Validate purchase capacity before creating any instances
        if (purchaseId is not null)
        {
            var purchases     = await purchaseSvc.GetAllAsync();
            var purchase      = purchases.FirstOrDefault(p => p.Id == purchaseId);
            var linkedCounts  = await purchaseSvc.GetLinkedCountsPerPurchaseAsync();
            var alreadyLinked = linkedCounts.TryGetValue(purchaseId, out var lc) ? lc : 0;
            var remaining     = purchase is not null ? purchase.Quantity - alreadyLinked : 0;

            if (remaining <= 0)
            {
                this.ToastError("That purchase has no remaining slots.");
                return await ReturnToConfigure(req);
            }
            if (req.Quantity > remaining)
            {
                this.ToastError($"That purchase only has {remaining} slot{(remaining == 1 ? "" : "s")} remaining.");
                return await ReturnToConfigure(req);
            }
        }

        for (var i = 0; i < req.Quantity; i++)
            await collection.AddInstanceAsync(req.CardId, req.Variant, locationId, purchaseId, req.ListIds);

        var wishlists = (await listSvc.GetAllAsync()).Where(l => l.Type == "Wishlist").ToList();
        foreach (var wl in wishlists)
            await listSvc.FulfillWishlistAsync(wl.Id);

        // Compute updated chips for OOB update on the card grid
        var variantCounts = await collection.GetVariantCountsForCardAsync(req.CardId);

        this.ToastSuccess($"Added {req.Quantity} × {(req.Variant == "FirstEdition" ? "1st Edition" : req.Variant)} to your collection.");
        return PartialView("~/Views/AddToCollection/_Success.cshtml",
            new AddToCollectionSuccessViewModel(req.CardId, req.Quantity, req.Variant, variantCounts));
    }

    private async Task<IActionResult> ReturnToConfigure(AddToCollectionRequest req)
        => await Configure(req.CardId);

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<AddToCollectionSetsViewModel> BuildSetsViewModel(string? q)
    {
        var allSets = await tcgDex.GetSetsAsync();
        var flat    = SetGrouping.Build(allSets).SelectMany(g => g.Sets).ToList();

        var ownedCounts = await collection.GetCountsAsync();
        var ownedSetIds = ownedCounts.Keys
            .Select(id => { var i = id.LastIndexOf('-'); return i > 0 ? id[..i] : id; })
            .ToHashSet();

        if (!string.IsNullOrWhiteSpace(q))
            flat = flat.Where(s =>
                s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                s.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        return new AddToCollectionSetsViewModel(q ?? "", flat, ownedSetIds);
    }

    private async Task<AddToCollectionPickViewModel> BuildCardsViewModel(string setId, string? q)
    {
        var setTask    = tcgDex.GetSetAsync(setId);
        var countsTask = collection.GetCountsAsync();
        await Task.WhenAll(setTask, countsTask);

        var cards = setTask.Result?.Cards ?? new();
        if (!string.IsNullOrWhiteSpace(q))
            cards = cards.Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.LocalId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        return new AddToCollectionPickViewModel(setId, setTask.Result?.Name ?? setId,
            q ?? "", cards, countsTask.Result);
    }
}
