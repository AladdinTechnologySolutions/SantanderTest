using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/stories")]
[Produces("application/json")]
public sealed class StoriesController(IBestStoriesService storiesService) : ControllerBase
{
    /// <summary>Returns the best <paramref name="n"/> Hacker News stories ordered by score descending.</summary>
    /// <param name="n">Number of stories to return (at least 1).</param>
    [HttpGet("best")]
    [ProducesResponseType<IReadOnlyList<StoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> GetBest([FromQuery] int? n, CancellationToken cancellationToken)
    {
        if (n is null or < 1)
        {
            ModelState.AddModelError(nameof(n), "n must be an integer greater than or equal to 1.");
            return ValidationProblem(ModelState);
        }

        try
        {
            return Ok(await storiesService.GetBestStoriesAsync(n.Value, cancellationToken));
        }
        catch (StoriesUnavailableException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
