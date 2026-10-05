using System.Collections.Concurrent;
using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Models;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Services;

/// <summary>
/// Holds an in-memory snapshot of all best stories, pre-sorted by score.
/// API requests are served from the snapshot only, so the load on Hacker News
/// depends on the refresh interval, not on incoming traffic.
/// </summary>
public sealed class BestStoriesCache(
    IHackerNewsClient client,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesCache> logger) : IBestStoriesService
{
    private readonly Lock _refreshLock = new();
    private volatile IReadOnlyList<StoryDto>? _snapshot;
    private Task? _inFlightRefresh;

    public async Task<IReadOnlyList<StoryDto>> GetBestStoriesAsync(int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var snapshot = _snapshot;
        if (snapshot is null)
        {
            // Cold start: wait for the shared load instead of starting a new one per request.
            try
            {
                await RefreshAsync().WaitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new StoriesUnavailableException("Best stories could not be loaded from Hacker News.", ex);
            }

            snapshot = _snapshot ?? throw new StoriesUnavailableException("Best stories are not available.");
        }

        return snapshot.Count <= count ? snapshot : snapshot.Take(count).ToArray();
    }

    public Task RefreshAsync()
    {
        lock (_refreshLock)
        {
            if (_inFlightRefresh is { IsCompleted: false })
            {
                return _inFlightRefresh;
            }

            // The load is shared by all callers, so it must not be tied to any single caller's token.
            _inFlightRefresh = LoadSnapshotAsync(CancellationToken.None);
            return _inFlightRefresh;
        }
    }

    private async Task LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var ids = await client.GetBestStoryIdsAsync(cancellationToken);
        var stories = new ConcurrentBag<StoryDto>();
        var failures = 0;

        await Parallel.ForEachAsync(
            ids,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Value.MaxConcurrentRequests,
                CancellationToken = cancellationToken,
            },
            async (id, ct) =>
            {
                try
                {
                    var item = await client.GetItemAsync(id, ct);
                    if (StoryDto.IsValidStory(item))
                    {
                        stories.Add(StoryDto.FromItem(item!));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // One bad item should not discard the whole refresh.
                    Interlocked.Increment(ref failures);
                    logger.LogWarning(ex, "Failed to fetch Hacker News item {ItemId}", id);
                }
            });

        if (ids.Count > 0 && failures == ids.Count)
        {
            // Keep the previous snapshot rather than replacing it with nothing.
            throw new HttpRequestException("All Hacker News item requests failed.");
        }

        _snapshot = stories
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Time)
            .ToArray();

        logger.LogInformation(
            "Best stories snapshot refreshed: {StoryCount} stories ({FailureCount} failed)", _snapshot.Count, failures);
    }
}
