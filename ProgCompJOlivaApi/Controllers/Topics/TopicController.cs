using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Topics.Dtos;
using ProgCompJOlivaApi.Data;

namespace ProgCompJOlivaApi.Controllers.Topics;

[ApiController]
[Route("api/topic")]
public class TopicController(AppDbContext db) : ControllerBase
{
    /// <summary>Lists all topics with how many problems each one has, for filter UIs.</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<TopicDto>>> GetAll(CancellationToken ct = default)
    {
        var topics = await db.Topics
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TopicDto
            {
                Id = t.Id,
                Name = t.Name,
                ProblemCount = t.Problems.Count
            })
            .ToListAsync(ct);

        return Ok(topics);
    }
}
