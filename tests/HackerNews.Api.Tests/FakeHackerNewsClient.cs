using HackerNews.Api.Clients;
using HackerNews.Api.Models;

namespace HackerNews.Api.Tests;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private int _idsCalls;
    private int _itemCalls;

    public List<long> Ids { get; } = [];
    public Dictionary<long, HackerNewsItem?> Items { get; } = [];
    public HashSet<long> FailingIds { get; } = [];
    public bool FailIds { get; set; }
    public TaskCompletionSource? Gate { get; set; }

    public int IdsCalls => _idsCalls;
    public int ItemCalls => _itemCalls;

    public FakeHackerNewsClient AddStory(long id, int score, string type = "story", bool deleted = false, bool dead = false)
    {
        Ids.Add(id);
        Items[id] = new HackerNewsItem
        {
            Id = id,
            Type = type,
            By = $"user{id}",
            Time = 1570887781 + id,
            Title = $"Story {id}",
            Url = $"https://example.com/{id}",
            Score = score,
            Descendants = (int)id * 2,
            Deleted = deleted,
            Dead = dead,
        };
        return this;
    }

    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _idsCalls);
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        if (FailIds)
        {
            throw new HttpRequestException("ids unavailable");
        }

        return Ids.ToArray();
    }

    public Task<HackerNewsItem?> GetItemAsync(long id, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _itemCalls);
        if (FailingIds.Contains(id))
        {
            throw new HttpRequestException($"item {id} unavailable");
        }

        return Task.FromResult(Items.GetValueOrDefault(id));
    }
}
