namespace ClintCardShop.Models;

public static class SetGrouping
{
    public static List<(string Serie, List<SetBrief> Sets)> Build(List<SetBrief> raw)
    {
        var named    = new List<(string serie, List<SetBrief> sets)>();
        var namedIdx = new Dictionary<string, int>();
        var ungrouped = new List<SetBrief>();

        foreach (var set in raw)
        {
            if (IsTcgpSet(set)) continue;
            var s = SerieOf(set);
            if (s is null) { ungrouped.Add(set); continue; }
            if (!namedIdx.TryGetValue(s, out var i))
            {
                i = named.Count;
                named.Add((s, new List<SetBrief>()));
                namedIdx[s] = i;
            }
            named[i].sets.Add(set);
        }

        named.Reverse();
        foreach (var g in named) g.sets.Reverse();
        if (ungrouped.Count > 0)
            named.Add(("other", ungrouped));

        return named.Select(g => (g.serie, g.sets)).ToList();
    }

    public static string? SerieOf(SetBrief set)
    {
        if (set.Logo is null) return null;
        var parts = set.Logo.Split('/');
        var en = Array.IndexOf(parts, "en");
        return en >= 0 && en + 1 < parts.Length ? parts[en + 1] : null;
    }

    private static bool IsTcgpSet(SetBrief set) =>
        SerieOf(set) == "tcgp" ||
        (set.Logo is null && set.Id.Length > 0 && char.IsUpper(set.Id[0]));

    public static readonly Dictionary<string, string> SeriesNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sv"]    = "Scarlet & Violet",
        ["swsh"]  = "Sword & Shield",
        ["sm"]    = "Sun & Moon",
        ["xy"]    = "XY",
        ["bw"]    = "Black & White",
        ["hgss"]  = "HeartGold SoulSilver",
        ["dp"]    = "Diamond & Pearl",
        ["ex"]    = "EX Series",
        ["ecard"] = "E-Card Series",
        ["neo"]   = "Neo Series",
        ["gym"]   = "Gym Series",
        ["tr"]    = "Team Rocket",
        ["base"]  = "Base Set Era",
        ["other"] = "Other",
    };

    public static string DisplayName(string slug) =>
        SeriesNames.TryGetValue(slug, out var name) ? name : slug.ToUpperInvariant();
}
