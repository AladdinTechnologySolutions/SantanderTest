using System.Net.Http.Json;
using HackerNews.Api.Models;

namespace HackerNews.Api.Clients;

/// <summary>
/// Typed HttpClient for the Hacker News Firebase API. BaseAddress and resilience
/// (retry, circuit breaker, timeouts) are configured in Program.cs.
/// </summary>
public sealed class HackerNewsClient(HttpClient httpClient) : IHackerNewsClient
{
    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<long[]>("beststories.json", cancellationToken) ?? [];

    // Hacker News returns the literal "null" for unknown ids, which deserializes to null.
    public Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", cancellationToken);
}
