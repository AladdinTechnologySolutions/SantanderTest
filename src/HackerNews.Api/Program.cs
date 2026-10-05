using HackerNews.Api.Clients;
using HackerNews.Api.Configuration;
using HackerNews.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<HackerNewsOptions>()
    .Bind(builder.Configuration.GetSection(HackerNewsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddHttpClient<IHackerNewsClient, HackerNewsClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<IBestStoriesService, BestStoriesCache>();
builder.Services.AddHostedService<BestStoriesRefreshService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program;
