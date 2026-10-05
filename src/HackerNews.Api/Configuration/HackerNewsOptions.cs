using System.ComponentModel.DataAnnotations;

namespace HackerNews.Api.Configuration;

public sealed class HackerNewsOptions
{
    public const string SectionName = "HackerNews";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    /// <summary>How often the best-stories snapshot is refreshed from Hacker News.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "1.00:00:00")]
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Upper bound on parallel item requests sent to Hacker News.</summary>
    [Range(1, 100)]
    public int MaxConcurrentRequests { get; set; } = 10;
}
