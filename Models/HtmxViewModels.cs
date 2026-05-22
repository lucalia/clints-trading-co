using ClintCardShop.Data;

namespace ClintCardShop.Models;

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

public record AddCardSetsViewModel(
    string Context,
    string CtxId,
    string Query,
    List<SetBrief> Sets,
    HashSet<string> OwnedSetIds
);

public record AddCardPickViewModel(
    string Context,
    string CtxId,
    string SetId,
    string SetName,
    List<CardBrief> Cards,
    Dictionary<string, int> CollectionCounts,
    string? SuccessMsg
);

public record AddCardConfigureViewModel(
    string Context,
    string CtxId,
    CardBrief Card,
    CardDetail? Detail,
    List<string> Variants,
    List<Location> Locations,
    int MaxCount,
    bool IsWishlist
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

public class AddCardSubmitRequest
{
    public string Context { get; set; } = "";
    public string CtxId { get; set; } = "";
    public string CardId { get; set; } = "";
    public string Variant { get; set; } = "Normal";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public int Count { get; set; } = 1;
}

public class CollectionAdjustRequest
{
    public string CardId { get; set; } = "";
    public string LocationId { get; set; } = CollectionDbContext.DefaultLocationId;
    public string Variant { get; set; } = "Normal";
    public int Delta { get; set; }
}
