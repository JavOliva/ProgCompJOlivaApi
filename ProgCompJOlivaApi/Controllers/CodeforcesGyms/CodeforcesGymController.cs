using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.CodeforcesGyms.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;

namespace ProgCompJOlivaApi.Controllers.CodeforcesGyms;

/// <summary>
/// Admin management of the Codeforces gym list — the set of gyms the system pulls problems from
/// and fetches standings for. <see cref="ImportGym"/> registers a gym and imports its problems in
/// one step; the rest is plain CRUD over the registry.
/// </summary>
[ApiController]
[Route("api/codeforces-gym")]
[Authorize(Roles = Constants.AdminRole)]
public class CodeforcesGymController(AppDbContext db, CodeforcesGymImporter importer) : ControllerBase
{
    /// <summary>
    /// Adds a Codeforces gym by id: registers it (enabled, so the solve sync starts tracking it)
    /// and imports its problems via the Codeforces API. Idempotent — re-importing only adds new
    /// problems. This makes one rate-limited Codeforces call, so the request may take a few seconds.
    /// </summary>
    [HttpPost("{gymContestId:int}/import")]
    public async Task<IActionResult> ImportGym(int gymContestId, CancellationToken ct = default)
    {
        if (gymContestId <= 0)
            return BadRequest(new { error = "Gym contest id must be a positive integer." });

        try
        {
            var result = await importer.ImportAsync(gymContestId, ct);
            return Ok(new
            {
                gymContestId = result.GymContestId,
                name = result.Name,
                addedProblems = result.AddedProblems,
                totalProblems = result.TotalProblems,
                gymWasNew = result.GymWasNew
            });
        }
        catch (InvalidOperationException ex)
        {
            // Missing credentials, or Codeforces returned an error/transient failure.
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Failed to reach Codeforces: {ex.Message}" });
        }
    }

    /// <summary>Lists the available fetch strategies (enum names), for selection UIs.</summary>
    [HttpGet("fetch-methods")]
    public ActionResult<List<string>> GetFetchMethods()
        => Ok(Enum.GetNames<GymFetchMethod>().ToList());

    [HttpGet]
    public async Task<ActionResult<List<CodeforcesGymDto>>> GetAll(
        [FromQuery] bool? onlyEnabled,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var query = db.CodeforcesGyms.AsNoTracking().AsQueryable();

        if (onlyEnabled == true)
            query = query.Where(g => g.Enabled);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var pattern = $"%{s}%";
            // Match the friendly name, or an exact gym id when the term is numeric.
            if (int.TryParse(s, out var gymId))
                query = query.Where(g => (g.Name != null && EF.Functions.ILike(g.Name, pattern)) || g.GymContestId == gymId);
            else
                query = query.Where(g => g.Name != null && EF.Functions.ILike(g.Name, pattern));
        }

        var gyms = await query
            .OrderBy(g => g.GymContestId)
            .ToListAsync(ct);

        return Ok(gyms.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CodeforcesGymDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var gym = await db.CodeforcesGyms
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (gym is null)
            return NotFound(new { error = "Gym not found." });

        return Ok(ToDto(gym));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCodeforcesGymRequest request, CancellationToken ct = default)
    {
        if (request.GymContestId <= 0)
            return BadRequest(new { error = "GymContestId must be a positive integer." });

        if (!TryParseFetchMethod(request.FetchMethod, out var fetchMethod, out var error))
            return BadRequest(new { error });

        var exists = await db.CodeforcesGyms.AnyAsync(g => g.GymContestId == request.GymContestId, ct);
        if (exists)
            return Conflict(new { error = "This gym is already registered." });

        var now = DateTime.UtcNow;
        var gym = new CodeforcesGym
        {
            Id = Guid.NewGuid(),
            GymContestId = request.GymContestId,
            Name = Normalize(request.Name),
            FetchMethod = fetchMethod,
            Enabled = request.Enabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.CodeforcesGyms.Add(gym);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = gym.Id }, ToDto(gym));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCodeforcesGymRequest request, CancellationToken ct = default)
    {
        var gym = await db.CodeforcesGyms.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gym is null)
            return NotFound(new { error = "Gym not found." });

        if (request.GymContestId.HasValue)
        {
            if (request.GymContestId.Value <= 0)
                return BadRequest(new { error = "GymContestId must be a positive integer." });

            if (request.GymContestId.Value != gym.GymContestId)
            {
                var clash = await db.CodeforcesGyms
                    .AnyAsync(g => g.GymContestId == request.GymContestId.Value && g.Id != id, ct);
                if (clash)
                    return Conflict(new { error = "This gym is already registered." });

                gym.GymContestId = request.GymContestId.Value;
            }
        }

        if (request.FetchMethod != null)
        {
            if (!TryParseFetchMethod(request.FetchMethod, out var fetchMethod, out var error))
                return BadRequest(new { error });

            gym.FetchMethod = fetchMethod;
        }

        if (request.Name != null)
            gym.Name = Normalize(request.Name);

        if (request.Enabled.HasValue)
            gym.Enabled = request.Enabled.Value;

        gym.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(gym));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var gym = await db.CodeforcesGyms.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (gym is null)
            return NotFound(new { error = "Gym not found." });

        db.CodeforcesGyms.Remove(gym);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Parses the fetch-method name (case-insensitive). A null/empty value defaults to
    /// <see cref="GymFetchMethod.Standings"/>.
    /// </summary>
    private static bool TryParseFetchMethod(string? value, out GymFetchMethod fetchMethod, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            fetchMethod = GymFetchMethod.Standings;
            return true;
        }

        if (Enum.TryParse(value.Trim(), ignoreCase: true, out fetchMethod)
            && Enum.IsDefined(fetchMethod))
            return true;

        fetchMethod = default;
        error = $"Unknown fetch method '{value}'. Allowed: {string.Join(", ", Enum.GetNames<GymFetchMethod>())}.";
        return false;
    }

    private static CodeforcesGymDto ToDto(CodeforcesGym g) => new()
    {
        Id = g.Id,
        GymContestId = g.GymContestId,
        Name = g.Name,
        FetchMethod = g.FetchMethod.ToString(),
        Enabled = g.Enabled,
        CreatedAtUtc = g.CreatedAtUtc,
        UpdatedAtUtc = g.UpdatedAtUtc
    };
}
