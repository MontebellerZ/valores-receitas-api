using System.Net.Http.Json;
using ValoresReceitas.Api.DTOs;

namespace ValoresReceitas.Api.Tests;

internal static class PageTestExtensions
{
    public static async Task<List<T>> GetPageItemsAsync<T>(this HttpClient client, string path)
    {
        var items = new List<T>();
        var separator = path.Contains('?') ? "&" : "?";
        for (var page = 1; ; page++)
        {
            var result = await client.GetFromJsonAsync<PageDto<T>>($"{path}{separator}page={page}&pageSize=100")
                ?? throw new InvalidOperationException("Missing page response.");
            items.AddRange(result.Items);
            if (page >= result.TotalPages) return items;
        }
    }
}
