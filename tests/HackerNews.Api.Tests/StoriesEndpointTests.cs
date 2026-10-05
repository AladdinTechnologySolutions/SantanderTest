using System.Net;
using System.Text.Json;
using HackerNews.Api.Clients;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HackerNews.Api.Tests;

public class StoriesEndpointTests
{
    private static HttpClient CreateClient(FakeHackerNewsClient fake) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHackerNewsClient>();
                services.AddSingleton<IHackerNewsClient>(fake);
            }))
            .CreateClient();

    [Fact]
    public async Task Returns_stories_in_expected_json_shape()
    {
        var fake = new FakeHackerNewsClient().AddStory(1, 10).AddStory(2, 20).AddStory(3, 30);
        using var http = CreateClient(fake);

        var response = await http.GetAsync("/api/stories/best?n=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stories = json.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, stories.Length);

        var first = stories[0];
        Assert.Equal("Story 3", first.GetProperty("title").GetString());
        Assert.Equal("https://example.com/3", first.GetProperty("uri").GetString());
        Assert.Equal("user3", first.GetProperty("postedBy").GetString());
        Assert.Equal("2019-10-12T13:43:04+00:00", first.GetProperty("time").GetString());
        Assert.Equal(30, first.GetProperty("score").GetInt32());
        Assert.Equal(6, first.GetProperty("commentCount").GetInt32());
        Assert.Equal(20, stories[1].GetProperty("score").GetInt32());
    }

    [Theory]
    [InlineData("/api/stories/best")]
    [InlineData("/api/stories/best?n=0")]
    [InlineData("/api/stories/best?n=-5")]
    [InlineData("/api/stories/best?n=abc")]
    public async Task Returns_bad_request_for_invalid_n(string url)
    {
        using var http = CreateClient(new FakeHackerNewsClient().AddStory(1, 10));

        var response = await http.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_service_unavailable_when_hacker_news_is_down()
    {
        using var http = CreateClient(new FakeHackerNewsClient { FailIds = true });

        var response = await http.GetAsync("/api/stories/best?n=5");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
