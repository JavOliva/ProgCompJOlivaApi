using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Problems.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Controllers.Problems;

[ApiController]
[Route("api/problem")]
public class ProblemController(AppDbContext db) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost("codeforces")]
    public async Task<IActionResult> CreateCodeforces([FromBody] CreateCodeforcesProblemRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        var contestProblemId = request.ContestProblemId.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title is required");

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("Url is required");

        if (string.IsNullOrWhiteSpace(contestProblemId))
            return BadRequest("Contest Problem Id is required");

        if (request.ContestId < 0)
            return BadRequest("Contest Id should be a non negative integer");

        var problemExists = await db.Problems
            .AnyAsync(x => (x.Title == title && x.Judge == "Codeforces" && (x.ContestId ?? -1) == request.ContestId && (x.ContestProblemId ?? "") == contestProblemId) || x.Url == url, ct);

        if (problemExists)
            return Conflict(new { error = "This problem already exists." });

        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Judge = "Codeforces",
            ContestId = request.ContestId,
            ContestProblemId = contestProblemId,
            ExternalId = $"{request.ContestId}/problem/{contestProblemId}",
            Title = title,
            Url = url,
            Difficulty = request.Difficulty,
            TagsJson = request.TagsJson,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Problems.Add(problem);
        await db.SaveChangesAsync(ct);

        return Ok(new CreateCodeforcesProblemResponse());
    }
}