using System.Text.Json;
using ClintCardShop.Data;
using ClintCardShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ClintCardShop.Services;

public class TcgDexService(HttpClient http, IDbContextFactory<CollectionDbContext> dbFactory)
{
    private const string BaseUrl = "https://api.tcgdex.net/v2/en";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SetResponse?> GetSetAsync(string setId) =>
        await GetCachedAsync<SetResponse>($"{BaseUrl}/sets/{setId}");

    public async Task<CardDetail?> GetCardAsync(string setId, string localId) =>
        await GetCachedAsync<CardDetail>($"{BaseUrl}/sets/{setId}/{localId}");

    public async Task<List<SetBrief>> GetSetsAsync() =>
        await GetCachedAsync<List<SetBrief>>($"{BaseUrl}/sets") ?? new();

    private async Task<T?> GetCachedAsync<T>(string url)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var cached = await db.ApiCache.FindAsync(url);

        if (cached != null && cached.CachedAt > DateTime.UtcNow - CacheTtl)
            return JsonSerializer.Deserialize<T>(cached.Body, JsonOptions);

        try
        {
            var response = await http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return cached != null ? JsonSerializer.Deserialize<T>(cached.Body, JsonOptions) : default;

            var body = await response.Content.ReadAsStringAsync();

            if (cached != null)
            {
                cached.Body = body;
                cached.CachedAt = DateTime.UtcNow;
            }
            else
            {
                db.ApiCache.Add(new ApiCacheEntry { Url = url, Body = body, CachedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();

            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch
        {
            // API unreachable — serve stale cache rather than failing
            return cached != null ? JsonSerializer.Deserialize<T>(cached.Body, JsonOptions) : default;
        }
    }
}
