using ClintCardShop.Data;

namespace ClintCardShop.Models;

// ── Add to Collection flow ────────────────────────────────────────────────

public record AddToCollectionSetsViewModel(
    string Query,
    List<SetBrief> Sets,
    HashSet<string> OwnedSetIds,
    string? PreListId = null,
    string? PrePurchaseId = null
);

public record AddToCollectionPickViewModel(
    string SetId,
    string SetName,
    string Query,
    List<CardBrief> Cards,
    Dictionary<string, int> OwnedCounts,
    string? PreListId = null,
    string? PrePurchaseId = null
);

public record AddToCollectionConfigureViewModel(
    CardBrief Card,
    CardDetail? Detail,
    List<string> Variants,
    List<Location> Locations,
    List<Purchase> Purchases,
    Dictionary<string, int> LinkedQty,
    List<CardList> StandardLists,
    string? PreListId = null,
    string? PrePurchaseId = null
);

public record AddToCollectionSuccessViewModel(
    string CardId,
    int Quantity,
    string Variant,
    Dictionary<string, int> VariantCounts  // variant → count, for OOB chip update
);

public class AddToCollectionRequest
{
    public string CardId    { get; set; } = "";
    public string Variant   { get; set; } = "Normal";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public int    Quantity  { get; set; } = 1;
    public string? PurchaseId { get; set; }
    public List<string> ListIds { get; set; } = new();
}

public record SetsSearchModel(
    string Query,
    List<(string Serie, List<SetBrief> Sets)> Groups,
    int TotalAll
);

public record CardGridViewModel(
    string SetId,
    List<CardBrief> Cards,
    Dictionary<string, int> VariantCounts,
    Dictionary<string, int> Counts,
    string Search,
    string Filter
);

public record CardModalViewModel(
    CardBrief? Card,
    CardDetail? Detail,
    Dictionary<string, int> LocationCounts,
    List<Location> Locations
);

public record CollectionSectionViewModel(
    string CardId,
    List<CardInstance> Instances,
    List<Location> Locations,
    int TotalOwned
);

public record LocationsListViewModel(
    List<Location> Locations,
    Dictionary<string, int> CardCounts
);

public record ListsListViewModel(
    List<CardList> Lists,
    Dictionary<string, int> ListCounts
);

public record PurchasesListViewModel(
    List<Purchase> Purchases,
    Dictionary<string, int> LinkedQty
);

public record DetailCardGridViewModel(
    List<CardBrief> Cards,
    Dictionary<string, int> VariantCounts,
    string? RemoveUrl,
    string? UnlinkUrl
);

public record ListSharePanelViewModel(
    string ListId,
    string ShareUrl
);

public record PurchaseCardsPanelViewModel(
    Purchase Purchase,
    List<(CardBrief Card, int Count)> CardBriefs,
    Dictionary<string, int> LinkedVariantCounts
);

public record ModalLinkPurchaseFormViewModel(
    string CardId,
    List<Purchase> Purchases,
    Dictionary<string, int> LinkedQty,
    List<Location> Locations,
    List<string> Variants
);

public record ModalAddToListFormViewModel(
    string CardId,
    List<CardList> Lists,
    List<Location> Locations,
    List<string> Variants
);

public class ModalLinkPurchaseRequest
{
    public string CardId { get; set; } = "";
    public string PurchaseId { get; set; } = "";
    public string Variant { get; set; } = "Normal";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public int Qty { get; set; } = 1;
}

public class ModalAddToListRequest
{
    public string CardId { get; set; } = "";
    public string ListId { get; set; } = "";
    public string Variant { get; set; } = "Normal";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public int Count { get; set; } = 1;
}

public record ModalListsTabViewModel(
    string CardId,
    List<(CardList List, List<(string Variant, int Count)> Chips)> CardLists
);

public record ModalPurchasesTabViewModel(
    string CardId,
    List<(Purchase P, CardInstance Instance)> CardPurchases,
    List<Purchase> AllPurchases,
    Dictionary<string, int> LinkedQty,
    List<Location> Locations,
    int TotalOwned
);

public class CollectionAdjustRequest
{
    public string CardId { get; set; } = "";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public string Variant { get; set; } = "Normal";
    public int Delta { get; set; }
}
