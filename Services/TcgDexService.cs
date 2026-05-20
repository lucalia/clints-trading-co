using System.Net.Http.Json;
using System.Text.Json;
using ClintCardShop.Models;

namespace ClintCardShop.Services;

public class TcgDexService(HttpClient http)
{
    private const string BaseUrl = "https://api.tcgdex.net/v2/en";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SetResponse?> GetSetAsync(string setId)
    {
        return await http.GetFromJsonAsync<SetResponse>($"{BaseUrl}/sets/{setId}", JsonOptions);
    }

    public async Task<CardDetail?> GetCardAsync(string setId, string localId)
    {
        return await http.GetFromJsonAsync<CardDetail>($"{BaseUrl}/sets/{setId}/{localId}", JsonOptions);
    }

    public async Task<List<SetBrief>> GetSetsAsync()
    {
        return await http.GetFromJsonAsync<List<SetBrief>>($"{BaseUrl}/sets", JsonOptions) ?? new();
    }
}
