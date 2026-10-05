namespace HackerNews.Api.Models;

/// <summary>
/// Public response contract for a single story.
/// </summary>
public sealed record StoryDto(
    string Title,
    string? Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount)
{
    public static StoryDto FromItem(HackerNewsItem item) => new(
        Title: item.Title ?? string.Empty,
        Uri: item.Url,
        PostedBy: item.By ?? string.Empty,
        Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
        Score: item.Score,
        CommentCount: item.Descendants ?? 0);

    /// <summary>
    /// Only live stories are exposed; deleted, dead and non-story items are skipped.
    /// </summary>
    public static bool IsValidStory(HackerNewsItem? item) =>
        item is { Deleted: false, Dead: false } &&
        string.Equals(item.Type, "story", StringComparison.OrdinalIgnoreCase);
}
