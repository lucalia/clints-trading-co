namespace ClintCardShop.Models;

public class SetResponse
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Logo { get; set; }
    public string? Symbol { get; set; }
    public CardCount CardCount { get; set; } = new();
    public List<CardBrief> Cards { get; set; } = new();
}

public class CardCount
{
    public int Official { get; set; }
    public int Total { get; set; }
}

public class CardBrief
{
    public string Id { get; set; } = "";
    public string LocalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Image { get; set; }
}

public class SetBrief
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Logo { get; set; }
    public string? Symbol { get; set; }
    public CardCount? CardCount { get; set; }
}
