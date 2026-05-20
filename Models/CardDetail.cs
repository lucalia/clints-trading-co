using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClintCardShop.Models;

// Handles Attack.Damage which the API returns as either a JSON number (10) or string ("80+")
public class NumberOrStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt32(out var i) ? i.ToString() : reader.GetDecimal().ToString(),
            JsonTokenType.String => reader.GetString(),
            _ => null
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

public class CardDetail
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LocalId { get; set; }
    public string? Category { get; set; }
    public string? Rarity { get; set; }
    public int? Hp { get; set; }
    public List<string>? Types { get; set; }
    public string? Stage { get; set; }
    public string? Illustrator { get; set; }
    public string? Image { get; set; }
    public int? Retreat { get; set; }
    public string? RegulationMark { get; set; }
    public string? Effect { get; set; }
    public string? TrainerType { get; set; }
    public List<Attack>? Attacks { get; set; }
    public List<Ability>? Abilities { get; set; }
    public Variants? Variants { get; set; }
    public Pricing? Pricing { get; set; }
    public SetRef? Set { get; set; }
}

public class Attack
{
    public string Name { get; set; } = "";
    public List<string> Cost { get; set; } = new();
    [JsonConverter(typeof(NumberOrStringConverter))]
    public string? Damage { get; set; }
    public string? Effect { get; set; }
}

public class Ability
{
    public string Name { get; set; } = "";
    public string? Effect { get; set; }
    public string? Type { get; set; }
}

public class Variants
{
    public bool Normal { get; set; }
    public bool Reverse { get; set; }
    public bool Holo { get; set; }
    public bool FirstEdition { get; set; }
    public bool WPromo { get; set; }
}

public class Pricing
{
    public CardMarket? Cardmarket { get; set; }
}

public class CardMarket
{
    public decimal? Avg { get; set; }
    public decimal? Low { get; set; }
    public decimal? Trend { get; set; }
}

public class SetRef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
