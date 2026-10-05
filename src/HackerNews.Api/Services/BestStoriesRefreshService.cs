using HackerNews.Api.Configuration;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Services;

/// <summary>
/// Refreshes the best-stories snapshot on a fixed interval, so requests stay fast
/// and Hacker News receives a steady, bounded amount of traffic.
/// </summary>
public sealed class BestStoriesRefreshService(
    IBestStoriesService storiesService,
    IOptions<HackerNewsOptions> options,
    ILogger<BestStoriesRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.RefreshInterval);

        do
        {
            try
            {
                await storiesService.RefreshAsync().WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Keep serving the last good snapshot and try again on the next tick.
                logger.LogError(ex, "Failed to refresh best stories snapshot");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
