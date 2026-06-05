using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Contests.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Controllers.Contests;

[ApiController]
[Route("api/contest")]
public class ContestController(AppDbContext db) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCodeforces([FromBody] CreateContestRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required");

        var contestExists = await db.Contests
            .AnyAsync(x => x.Name == name, ct);

        if (contestExists)
            return Conflict(new { error = "This contest name already exists" });

        var contest = new Contest
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Contests.Add(contest);
        await db.SaveChangesAsync(ct);

        return Ok(new CreateContestResponse());
    }
}