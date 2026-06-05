using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Trainings.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Controllers.Trainings;

[ApiController]
[Route("api/training")]
public class TrainingController(AppDbContext db) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTrainingRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Name is required");

        var contestExists = await db.Trainings
            .AnyAsync(x => x.Name == name, ct);

        if (contestExists)
            return Conflict(new { error = "This Training name already exists" });

        var training = new Training
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        db.Trainings.Add(training);
        await db.SaveChangesAsync(ct);

        return Ok(new CreateTrainingResponse());
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        return Ok();
    }
}