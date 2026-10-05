using HackerNews.Api.Models;

namespace HackerNews.Api.Services;

public interface IBestStoriesService
{
    /// <summary>Returns up to <paramref name="count"/> best stories ordered by score descending.</summary>
    /// <exception cref="StoriesUnavailableException">No data could be loaded from Hacker News.</exception>
    Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Reloads the snapshot from Hacker News. Concurrent callers share a single load.</summary>
    Task RefreshAsync();
}

public sealed class StoriesUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
