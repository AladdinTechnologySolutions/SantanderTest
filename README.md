# Hacker News Best Stories API

An ASP.NET Core (.NET 9) REST API that returns the best *n* stories from the
[Hacker News API](https://github.com/HackerNews/API), ordered by score (highest first).

## Running the application

Prerequisite: the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet run --project src/HackerNews.Api --launch-profile http
```

The API listens on `http://localhost:5102`.

```bash
curl "http://localhost:5102/api/stories/best?n=10"
```

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

| Response | When |
|---|---|
| `200 OK` | An array of up to `n` stories, sorted by score descending |
| `400 Bad Request` | `n` is missing, not an integer, or less than 1 (ProblemDetails) |
| `503 Service Unavailable` | No data has been loaded yet and Hacker News can't be reached |

In Development, the OpenAPI document is served at `/openapi/v1.json`. `src/HackerNews.Api/HackerNews.Api.http` has a sample request you can run from VS or VS Code.

### Tests

```bash
dotnet test
```

The tests cover sorting and top-*n* selection, field mapping, filtering of invalid items,
request coalescing, keeping the last good data when Hacker News fails, and the HTTP contract
(JSON shape, validation, 503).

### Configuration (`appsettings.json`)

```json
"HackerNews": {
  "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
  "RefreshInterval": "00:01:00",
  "MaxConcurrentRequests": 10
}
```

Options are validated on startup.

## Design

```
client ──► StoriesController ──► BestStoriesCache (in-memory, pre-sorted snapshot)
                                        ▲
             BestStoriesRefreshService ─┘  every RefreshInterval
                                        │
                     HackerNewsClient (typed HttpClient + retry / circuit breaker / timeouts)
                                        │
                                 Hacker News API
```

**How Hacker News is protected from overload.** Incoming requests never reach Hacker News.
They are served from an immutable in-memory snapshot of all best stories, already sorted by score.
A background service rebuilds the snapshot on a fixed interval, so outbound traffic is constant:
about 201 calls per `RefreshInterval` (1 ID list plus up to 200 items), however many requests the API gets.

- **Single-flight loading.** If a request arrives before the first snapshot is ready, it waits on the
  in-flight load instead of starting its own. Many concurrent callers cause exactly one load.
- **Bounded parallelism.** Item fetches run with at most `MaxConcurrentRequests` in parallel.
- **Resilience.** `Microsoft.Extensions.Http.Resilience` adds retries with backoff, a circuit breaker
  and timeouts. If a refresh fails, the previous snapshot keeps being served. If a single item fails,
  it is skipped rather than discarding the whole refresh.
- **Cheap reads.** Serving a request is just `Take(n)` on an array, with no locks on the hot path.

## Assumptions

- **"Best" means sorted by `score`.** The IDs from `beststories.json` are in Hacker News' own ranking
  order, which is not strictly by score. The service therefore loads every ID in the list (currently up
  to 200), sorts by score, and returns the top *n*. Ties are broken by most recent first.
- **Freshness.** Data up to `RefreshInterval` old (default 1 minute) is acceptable. Scores on Hacker News
  change gradually, so this is a reasonable trade-off for protecting the upstream API.
- **Large `n`.** If `n` is larger than the number of available stories, all available stories are returned
  rather than an error.
- **Field mapping.** `uri` ← `url` (may be `null` for text posts such as Ask HN), `postedBy` ← `by`,
  `time` ← `time` (Unix seconds, returned as UTC ISO-8601), `commentCount` ← `descendants`.
- **Skipped items.** Deleted, dead, missing (`null`) and non-`story` items (for example jobs) are left out.
- **Single instance.** The cache is in-process. Each instance refreshes on its own, so N instances put
  N times the (still bounded) load on Hacker News.

## Enhancements given more time

- **Distributed cache** (for example Redis), refreshed by a single leader or worker, so a scaled-out
  deployment hits Hacker News once per interval in total.
- **Incremental refresh.** Re-fetch only new IDs on each tick and refresh existing items less often,
  or switch to the Firebase streaming API to receive pushed updates.
- **Inbound rate limiting** per client (ASP.NET Core `RateLimiter`) and HTTP caching headers
  (`Cache-Control` / `ETag`) or output caching, so clients and proxies can cache responses too.
- **Health checks** (`/health`) that report snapshot age and the last refresh result, plus metrics
  (OpenTelemetry) for refresh duration, failures and upstream call counts.
- An optional **upper limit on `n`** and **pagination** (`offset`) if the API contract allows it.
- **API versioning**, a Dockerfile / container image, and a CI pipeline (build, test, coverage).
- **Integration tests** against a stubbed HTTP server (for example WireMock.Net) to exercise the
  resilience pipeline end to end.
