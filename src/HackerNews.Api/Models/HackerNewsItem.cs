using System.Text.Json.Serialization;

namespace HackerNews.Api.Models;

/// <summary>
/// Raw item as returned by https://hacker-news.firebaseio.com/v0/item/{id}.json.
/// </summary>
public sealed record HackerNewsItem
{
    [JsonPropertyName("id")] public long Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("by")] public string? By { get; init; }
    [JsonPropertyName("time")] public long Time { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("score")] public int Score { get; init; }
    [JsonPropertyName("descendants")] public int? Descendants { get; init; }
    [JsonPropertyName("deleted")] public bool Deleted { get; init; }
    [JsonPropertyName("dead")] public bool Dead { get; init; }
}
