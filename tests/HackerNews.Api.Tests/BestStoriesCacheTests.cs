using HackerNews.Api.Configuration;
using HackerNews.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Tests;

public class BestStoriesCacheTests
{
    private static BestStoriesCache CreateCache(FakeHackerNewsClient client) =>
        new(client, Options.Create(new HackerNewsOptions { MaxConcurrentRequests = 4 }), NullLogger<BestStoriesCache>.Instance);

    [Fact]
    public async Task Returns_top_n_stories_ordered_by_score_descending()
    {
        var client = new FakeHackerNewsClient()
            .AddStory(1, score: 50)
            .AddStory(2, score: 300)
            .AddStory(3, score: 10)
            .AddStory(4, score: 120);
        var cache = CreateCache(client);

        var stories = await cache.GetBestStoriesAsync(3);

        Assert.Equal([300, 120, 50], stories.Select(s => s.Score));
    }

    [Fact]
    public async Task Returns_all_stories_when_n_exceeds_available()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 5).AddStory(2, 7);
        var cache = CreateCache(client);

        var stories = await cache.GetBestStoriesAsync(100);

        Assert.Equal(2, stories.Count);
    }

    [Fact]
    public async Task Maps_hacker_news_item_to_story_dto()
    {
        var client = new FakeHackerNewsClient().AddStory(21233041, 1716);
        client.Items[21233041] = client.Items[21233041]! with
        {
            Title = "A uBlock Origin update was rejected from the Chrome Web Store",
            Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            By = "ismaildonmez",
            Time = 1570887781,
            Descendants = 572,
        };
        var cache = CreateCache(client);

        var story = Assert.Single(await cache.GetBestStoriesAsync(1));

        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", story.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", story.Uri);
        Assert.Equal("ismaildonmez", story.PostedBy);
        Assert.Equal(new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero), story.Time);
        Assert.Equal(1716, story.Score);
        Assert.Equal(572, story.CommentCount);
    }

    [Fact]
    public async Task Skips_missing_deleted_dead_non_story_and_failing_items()
    {
        var client = new FakeHackerNewsClient()
            .AddStory(1, 100)
            .AddStory(2, 200, deleted: true)
            .AddStory(3, 300, dead: true)
            .AddStory(4, 400, type: "job")
            .AddStory(5, 500);
        client.Ids.Add(6); // id with no item (Hacker News returns null)
        client.FailingIds.Add(5);
        var cache = CreateCache(client);

        var story = Assert.Single(await cache.GetBestStoriesAsync(10));

        Assert.Equal(100, story.Score);
    }

    [Fact]
    public async Task Concurrent_requests_share_a_single_load()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 10).AddStory(2, 20);
        client.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = CreateCache(client);

        var requests = Enumerable.Range(0, 50).Select(_ => cache.GetBestStoriesAsync(2)).ToArray();
        client.Gate.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(1, client.IdsCalls);
        Assert.Equal(2, client.ItemCalls);
    }

    [Fact]
    public async Task Serves_from_snapshot_without_calling_hacker_news_again()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 10);
        var cache = CreateCache(client);

        for (var i = 0; i < 100; i++)
        {
            await cache.GetBestStoriesAsync(1);
        }

        Assert.Equal(1, client.IdsCalls);
        Assert.Equal(1, client.ItemCalls);
    }

    [Fact]
    public async Task Keeps_previous_snapshot_when_refresh_fails()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 10);
        var cache = CreateCache(client);
        await cache.RefreshAsync();

        client.FailIds = true;
        await Assert.ThrowsAsync<HttpRequestException>(cache.RefreshAsync);

        var story = Assert.Single(await cache.GetBestStoriesAsync(1));
        Assert.Equal(10, story.Score);
    }

    [Fact]
    public async Task Keeps_previous_snapshot_when_every_item_fails()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 10);
        var cache = CreateCache(client);
        await cache.RefreshAsync();

        client.FailingIds.Add(1);
        await Assert.ThrowsAsync<HttpRequestException>(cache.RefreshAsync);

        Assert.Single(await cache.GetBestStoriesAsync(1));
    }

    [Fact]
    public async Task Refresh_picks_up_new_scores()
    {
        var client = new FakeHackerNewsClient().AddStory(1, 10).AddStory(2, 20);
        var cache = CreateCache(client);
        await cache.RefreshAsync();

        client.Items[1] = client.Items[1]! with { Score = 99 };
        await cache.RefreshAsync();

        Assert.Equal(99, (await cache.GetBestStoriesAsync(1))[0].Score);
    }

    [Fact]
    public async Task Throws_unavailable_when_cold_load_fails()
    {
        var client = new FakeHackerNewsClient { FailIds = true };
        var cache = CreateCache(client);

        await Assert.ThrowsAsync<StoriesUnavailableException>(() => cache.GetBestStoriesAsync(1));
    }
}
