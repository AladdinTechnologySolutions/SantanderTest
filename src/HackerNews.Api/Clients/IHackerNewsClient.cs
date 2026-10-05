using HackerNews.Api.Models;

namespace HackerNews.Api.Clients;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);

    /// <returns>The item, or <c>null</c> if Hacker News has no item with that id.</returns>
    Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default);
}
