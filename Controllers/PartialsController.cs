using System.Text.RegularExpressions;
using ClintCardShop.Data;
using ClintCardShop.Models;
using ClintCardShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClintCardShop.Controllers;

[Route("partials")]
[Authorize]
public class PartialsController(
    TcgDexService tcgDex,
    CollectionService collection,
    LocationService locationSvc,
    CardListService listSvc,
    PurchaseService purchaseSvc) : Controller
{
    // ── Sets search ──────────────────────────────────────────────────────

    [HttpGet("sets")]
    public async Task<IActionResult> Sets(string? q)
    {
        var raw    = await tcgDex.GetSetsAsync();
        var groups = SetGrouping.Build(raw);

        if (!string.IsNullOrWhiteSpace(q))
        {
            groups = groups
                .Select(g => (g.Serie, Sets: g.Sets
                    .Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                s.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList()))
                .Where(g => g.Sets.Count > 0)
                .ToList();
        }

        return PartialView("~/Views/Partials/_SetsGroups.cshtml",
            new SetsSearchModel(q ?? "", groups, raw.Count));
    }

    // ── Card grid ────────────────────────────────────────────────────────

    [HttpGet("cards/{setId}")]
    public async Task<IActionResult> Cards(string setId, string? q, string? filter)
    {
        var setTask     = tcgDex.GetSetAsync(setId);
        var variantTask = collection.GetAllVariantCountsAsync();
        await Task.WhenAll(setTask, variantTask);

        var variantCounts = variantTask.Result;
        var counts = variantCounts
            .GroupBy(kv => kv.Key.Split(':')[0])
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        var cards = setTask.Result?.Cards ?? new();
        if (!string.IsNullOrWhiteSpace(q))
            cards = cards.Where(c =>
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.LocalId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filter == "owned")
            cards = cards.Where(c => counts.GetValueOrDefault(c.Id) > 0).ToList();
        else if (filter == "missing")
            cards = cards.Where(c => counts.GetValueOrDefault(c.Id) == 0).ToList();

        return PartialView("~/Views/Partials/_CardGrid.cshtml",
            new CardGridViewModel(setId, cards, variantCounts, counts, q ?? "", filter ?? "all"));
    }

    // ── Card modal (anonymous-accessible — card data is public) ─────────

    [AllowAnonymous]
    [HttpGet("card-modal/{cardId}")]
    public async Task<IActionResult> CardModal(string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx]    : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;

        var detailTask = tcgDex.GetCardAsync(setId, localId);
        var setTask    = tcgDex.GetSetAsync(setId);

        // Only load collection data for authenticated users
        Task<Dictionary<string, int>> variantTask;
        Task<List<Location>> locationsTask;
        if (User.Identity?.IsAuthenticated == true)
        {
            variantTask   = collection.GetVariantCountsForCardAsync(cardId);
            locationsTask = locationSvc.GetAllAsync();
        }
        else
        {
            variantTask   = Task.FromResult(new Dictionary<string, int>());
            locationsTask = Task.FromResult(new List<Location>());
        }

        await Task.WhenAll(detailTask, setTask, variantTask, locationsTask);

        var card = setTask.Result?.Cards.FirstOrDefault(c => c.Id == cardId);
        return PartialView("~/Views/Partials/_CardModal.cshtml",
            new CardModalViewModel(card, detailTask.Result, variantTask.Result, locationsTask.Result));
    }

    [AllowAnonymous]
    [HttpGet("modal-tab/{cardId}/info")]
    public async Task<IActionResult> ModalTabInfo(string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx]    : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;
        var detail  = await tcgDex.GetCardAsync(setId, localId);
        return PartialView("~/Views/Partials/_InfoSection.cshtml", detail);
    }

    [HttpGet("modal-tab/{cardId}/collection")]
    public async Task<IActionResult> ModalTabCollection(string cardId)
    {
        var instancesTask = collection.GetByCardAsync(cardId);
        var locationsTask = locationSvc.GetAllAsync();
        await Task.WhenAll(instancesTask, locationsTask);

        var instances = instancesTask.Result;
        var locations = locationsTask.Result;
        var total     = instances.Count;

        return PartialView("~/Views/Partials/_CollectionSection.cshtml",
            new CollectionSectionViewModel(cardId, instances, locations, total));
    }

    [HttpGet("modal-tab/{cardId}/lists")]
    public async Task<IActionResult> ModalTabLists(string cardId)
    {
        var allListsTask = listSvc.GetAllAsync();
        var instancesTask = collection.GetByCardAsync(cardId);
        await Task.WhenAll(allListsTask, instancesTask);

        var allLists  = allListsTask.Result;
        var instances = instancesTask.Result;

        // For each list, find which instances of this card belong to it
        var standardLists = allLists.Where(l => l.Type != "Wishlist").ToList();
        var cardLists = new List<(Data.CardList List, List<(string Variant, int Count)> Chips)>();

        foreach (var list in standardLists)
        {
            var listInstances = await listSvc.GetInstancesByListAsync(list.Id);
            var cardInList = listInstances.Where(i => i.CardId == cardId).ToList();
            if (cardInList.Any())
            {
                var chips = cardInList
                    .GroupBy(i => i.Variant)
                    .Select(g => (Variant: g.Key, Count: g.Count()))
                    .OrderBy(x => x.Variant)
                    .ToList();
                cardLists.Add((list, chips));
            }
        }

        return PartialView("~/Views/Partials/_ModalListsTab.cshtml",
            new ModalListsTabViewModel(cardId, cardLists));
    }

    [HttpGet("modal-tab/{cardId}/purchases")]
    public async Task<IActionResult> ModalTabPurchases(string cardId)
    {
        var byCardTask  = purchaseSvc.GetInstancesByCardAsync(cardId);
        var allTask     = purchaseSvc.GetAllAsync();
        var linkedTask  = purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        var locTask     = locationSvc.GetAllAsync();
        var instancesTask = collection.GetByCardAsync(cardId);
        await Task.WhenAll(byCardTask, allTask, linkedTask, locTask, instancesTask);

        var totalOwned = instancesTask.Result.Count;

        return PartialView("~/Views/Partials/_ModalPurchasesTab.cshtml",
            new ModalPurchasesTabViewModel(cardId, byCardTask.Result, allTask.Result,
                linkedTask.Result, locTask.Result, totalOwned));
    }

    // ── Modal: link to purchase ──────────────────────────────────────────

    [HttpGet("modal-tab/{cardId}/purchases/link-form")]
    public async Task<IActionResult> ModalPurchaseLinkForm(string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx] : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;

        var purchasesTask = purchaseSvc.GetAllAsync();
        var linkedTask    = purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        var locsTask      = locationSvc.GetAllAsync();
        var detailTask    = tcgDex.GetCardAsync(setId, localId);
        await Task.WhenAll(purchasesTask, linkedTask, locsTask, detailTask);

        return PartialView("~/Views/Partials/_ModalPurchaseLinkForm.cshtml",
            new ModalLinkPurchaseFormViewModel(cardId, purchasesTask.Result,
                linkedTask.Result, locsTask.Result, AvailableVariants(detailTask.Result)));
    }

    [HttpPost("modal-tab/{cardId}/purchases/submit-link")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModalPurchaseSubmitLink(string cardId, [FromForm] ModalLinkPurchaseRequest req)
    {
        if (string.IsNullOrEmpty(req.PurchaseId) || req.Qty <= 0)
            return await ModalTabPurchases(cardId);

        // Check purchase has enough remaining capacity
        var purchases        = await purchaseSvc.GetAllAsync();
        var purchase         = purchases.FirstOrDefault(p => p.Id == req.PurchaseId);
        var linkedCounts     = await purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        var alreadyLinked    = linkedCounts.TryGetValue(req.PurchaseId, out var lc) ? lc : 0;
        var remainingSlots   = purchase is not null ? purchase.Quantity - alreadyLinked : 0;

        if (remainingSlots <= 0)
        {
            this.ToastError("That purchase has no remaining slots.");
            return await ModalTabPurchases(cardId);
        }
        if (req.Qty > remainingSlots)
        {
            this.ToastError($"Only {remainingSlots} slot{(remainingSlots == 1 ? "" : "s")} remaining in that purchase.");
            return await ModalTabPurchases(cardId);
        }

        // Check user has enough unlinked instances of this card+variant
        var instances   = await collection.GetByCardAsync(cardId);
        var available   = instances.Where(i => i.Variant == req.Variant && i.PurchaseId == null).ToList();

        if (available.Count == 0)
        {
            this.ToastError($"You have no unlinked {req.Variant} copies of this card in your collection.");
            return await ModalTabPurchases(cardId);
        }
        if (req.Qty > available.Count)
        {
            this.ToastWarning($"Only {available.Count} unlinked {req.Variant} {(available.Count == 1 ? "copy" : "copies")} available — linked {available.Count}.");
            foreach (var inst in available)
                await purchaseSvc.LinkInstanceAsync(inst.Id, req.PurchaseId);
            return await ModalTabPurchases(cardId);
        }

        foreach (var inst in available.Take(req.Qty))
            await purchaseSvc.LinkInstanceAsync(inst.Id, req.PurchaseId);

        this.ToastSuccess($"Linked {req.Qty} {req.Variant} {(req.Qty == 1 ? "copy" : "copies")} to purchase.");
        return await ModalTabPurchases(cardId);
    }

    // ── Modal: add to list ───────────────────────────────────────────────

    [HttpGet("modal-tab/{cardId}/lists/add-form")]
    public async Task<IActionResult> ModalAddToListForm(string cardId)
    {
        var idx     = cardId.LastIndexOf('-');
        var setId   = idx > 0 ? cardId[..idx] : cardId;
        var localId = idx > 0 ? cardId[(idx + 1)..] : cardId;

        var listsTask  = listSvc.GetAllAsync();
        var locsTask   = locationSvc.GetAllAsync();
        var detailTask = tcgDex.GetCardAsync(setId, localId);
        await Task.WhenAll(listsTask, locsTask, detailTask);

        return PartialView("~/Views/Partials/_ModalAddToListForm.cshtml",
            new ModalAddToListFormViewModel(cardId, listsTask.Result,
                locsTask.Result, AvailableVariants(detailTask.Result)));
    }

    [HttpPost("modal-tab/{cardId}/lists/submit-add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModalAddToListSubmit(string cardId, [FromForm] ModalAddToListRequest req)
    {
        req.CardId = cardId;
        if (!string.IsNullOrEmpty(req.ListId) && req.Count > 0)
        {
            for (int i = 0; i < req.Count; i++)
            {
                var inst = await collection.AddInstanceAsync(cardId, req.Variant, req.LocationId, null, new[] { req.ListId });
            }
        }
        return await ModalTabLists(cardId);
    }

    // ── Collection adjust ────────────────────────────────────────────────

    [HttpPost("collection/adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectionAdjust([FromForm] CollectionAdjustRequest req)
    {
        // Old +/- adjust is no longer supported; redirect to collection tab view
        return await ModalTabCollection(req.CardId);
    }

    // ── Collection remove instance ────────────────────────────────────────

    [HttpPost("collection/remove-instance/{instanceId}/for-card/{cardId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CollectionRemoveInstanceForCard(string instanceId, string cardId)
    {
        await collection.RemoveInstanceAsync(instanceId);
        return await ModalTabCollection(cardId);
    }

    // ── Locations CRUD ───────────────────────────────────────────────────

    [HttpPost("locations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLocation([FromForm] string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            await locationSvc.AddAsync(name.Trim());
        return await LocationsListResult();
    }

    [HttpGet("locations/{id}/edit")]
    public async Task<IActionResult> EditLocationRow(string id)
    {
        var loc = (await locationSvc.GetAllAsync()).FirstOrDefault(l => l.Id == id);
        if (loc is null) return NotFound();
        return PartialView("~/Views/Partials/_LocationEditRow.cshtml", loc);
    }

    [HttpPost("locations/{id}/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLocation(string id, [FromForm] string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            await locationSvc.UpdateAsync(id, name.Trim());
        return await LocationsListResult();
    }

    [HttpPost("locations/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLocation(string id)
    {
        await locationSvc.DeleteAsync(id);
        return await LocationsListResult();
    }

    private async Task<IActionResult> LocationsListResult()
    {
        var locsTask   = locationSvc.GetAllAsync();
        var countsTask = collection.GetCountsPerLocationAsync();
        await Task.WhenAll(locsTask, countsTask);
        return PartialView("~/Views/Partials/_LocationsList.cshtml",
            new LocationsListViewModel(locsTask.Result, countsTask.Result));
    }

    // ── Lists CRUD ───────────────────────────────────────────────────────

    [HttpPost("lists")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddList([FromForm] string name, [FromForm] string type)
    {
        if (!string.IsNullOrWhiteSpace(name))
            await listSvc.AddAsync(name.Trim(), type);
        return await ListsListResult();
    }

    [HttpPost("lists/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteList(string id)
    {
        await listSvc.DeleteAsync(id);
        return await ListsListResult();
    }

    private async Task<IActionResult> ListsListResult()
    {
        var listsTask  = listSvc.GetAllAsync();
        var countsTask = listSvc.GetUniqueCardCountsPerListAsync();
        await Task.WhenAll(listsTask, countsTask);
        return PartialView("~/Views/Partials/_ListsList.cshtml",
            new ListsListViewModel(listsTask.Result, countsTask.Result));
    }

    // ── Purchases ────────────────────────────────────────────────────────

    [HttpGet("purchases/add-form")]
    public IActionResult PurchasesAddForm()
    {
        return PartialView("~/Views/Partials/_PurchaseAddForm.cshtml",
            new Purchase { PurchasedAt = DateTime.Today, Quantity = 1, Type = "Other" });
    }

    [HttpPost("purchases")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchase([FromForm] Purchase p)
    {
        if (!string.IsNullOrWhiteSpace(p.Source) && !string.IsNullOrWhiteSpace(p.Description))
        {
            if (p.Quantity < 1) p.Quantity = 1;
            await purchaseSvc.AddAsync(p);
        }
        return await PurchasesListResult();
    }

    [HttpPost("purchases/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePurchase(string id)
    {
        await purchaseSvc.DeleteAsync(id);
        return await PurchasesListResult();
    }

    private async Task<IActionResult> PurchasesListResult()
    {
        var pTask = purchaseSvc.GetAllAsync();
        var qTask = purchaseSvc.GetLinkedCountsPerPurchaseAsync();
        await Task.WhenAll(pTask, qTask);
        return PartialView("~/Views/Partials/_PurchasesList.cshtml",
            new PurchasesListViewModel(pTask.Result, qTask.Result));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    internal static List<string> AvailableVariants(CardDetail? detail)
    {
        if (detail?.Variants is not { } v) return ["Normal"];
        var list = new List<string>();
        if (v.Normal)       list.Add("Normal");
        if (v.Reverse)      list.Add("Reverse");
        if (v.Holo)         list.Add("Holo");
        if (v.FirstEdition) list.Add("FirstEdition");
        if (v.WPromo)       list.Add("WPromo");
        return list.Count > 0 ? list : ["Normal"];
    }

    internal static readonly Regex EnergyRegex = new(@"\{([A-Z])\}", RegexOptions.Compiled);

    internal static string RenderEffectText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var encoded = System.Net.WebUtility.HtmlEncode(text);
        return EnergyRegex.Replace(encoded, m => m.Groups[1].Value switch
        {
            "G" => "<span class=\"energy-coin energy-grass\" title=\"Grass\"></span>",
            "R" => "<span class=\"energy-coin energy-fire\" title=\"Fire\"></span>",
            "W" => "<span class=\"energy-coin energy-water\" title=\"Water\"></span>",
            "L" => "<span class=\"energy-coin energy-lightning\" title=\"Lightning\"></span>",
            "P" => "<span class=\"energy-coin energy-psychic\" title=\"Psychic\"></span>",
            "F" => "<span class=\"energy-coin energy-fighting\" title=\"Fighting\"></span>",
            "D" => "<span class=\"energy-coin energy-darkness\" title=\"Darkness\"></span>",
            "M" => "<span class=\"energy-coin energy-metal\" title=\"Metal\"></span>",
            "N" => "<span class=\"energy-coin\" style=\"background:#7038f8\" title=\"Dragon\">N</span>",
            "Y" => "<span class=\"energy-coin\" style=\"background:#f5a2c8\" title=\"Fairy\">Y</span>",
            "C" => "<span class=\"energy-coin\" style=\"background:#9ba7b0\" title=\"Colorless\">C</span>",
            _   => m.Value
        });
    }

    internal static string TypeColor(string type) => type switch
    {
        "Grass"     => "#78c850",
        "Fire"      => "#f08030",
        "Water"     => "#6890f0",
        "Lightning" => "#f8d030",
        "Psychic"   => "#f85888",
        "Fighting"  => "#c03028",
        "Darkness"  => "#705848",
        "Metal"     => "#b8b8d0",
        "Dragon"    => "#7038f8",
        "Fairy"     => "#f5a2c8",
        "Colorless" => "#9ba7b0",
        _           => "#6b7280"
    };

    internal static bool HasEnergyImage(string type) => type is
        "Grass" or "Fire" or "Water" or "Lightning" or "Psychic"
        or "Fighting" or "Darkness" or "Metal";

    internal static string VariantShort(string v) => v switch
    {
        "Normal"       => "NRM",
        "Reverse"      => "REV",
        "Holo"         => "HOLO",
        "FirstEdition" => "1st",
        "WPromo"       => "PROMO",
        _              => v
    };

    private const decimal EurToUsd = 1.09m;
    internal static string ToUsd(decimal eur) => (eur * EurToUsd).ToString("F2");
}
