using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Common;
using ProgCompJOlivaApi.Controllers.Contests.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Controllers.Contests;

[ApiController]
[Route("api/contest")]
public class ContestController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>Search contests by name, paginated. For the admin "manage contests" view.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ContestListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] bool? onlyActive,
        [FromQuery] string sortDir = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Contests.AsNoTracking().AsQueryable();

        if (onlyActive == true)
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern));
        }

        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ContestListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ProblemCount = c.ContestProblems.Count,
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<ContestListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContestDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var contest = await db.Contests
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ContestDetailDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                CreatedAtUtc = c.CreatedAtUtc,
                UpdatedAtUtc = c.UpdatedAtUtc,
                Problems = c.ContestProblems
                    .OrderBy(cp => cp.Position)
                    .Select(cp => new ContestProblemDto
                    {
                        Position = cp.Position,
                        ProblemId = cp.ProblemId,
                        Judge = cp.Problem.Judge,
                        Title = cp.Problem.Title,
                        Url = cp.Problem.Url,
                        ExternalId = cp.Problem.ExternalId,
                        Difficulty = cp.Problem.Difficulty
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        return Ok(contest);
    }

    /// <summary>Creates a contest, optionally from an ordered list of problems.</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContestRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Name is required." });

        var contestExists = await db.Contests.AnyAsync(x => x.Name == name, ct);
        if (contestExists)
            return Conflict(new { error = "This contest name already exists." });

        // De-duplicate while preserving the requested order.
        var orderedProblemIds = request.ProblemIds.Distinct().ToList();

        if (orderedProblemIds.Count > 0)
        {
            var found = await db.Problems
                .Where(p => orderedProblemIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(ct);

            var missing = orderedProblemIds.Except(found).ToList();
            if (missing.Count > 0)
                return BadRequest(new { error = "Some problems do not exist.", missing });
        }

        var now = DateTime.UtcNow;
        var contest = new Contest
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        for (var i = 0; i < orderedProblemIds.Count; i++)
        {
            contest.ContestProblems.Add(new ContestProblem
            {
                Id = Guid.NewGuid(),
                ContestId = contest.Id,
                ProblemId = orderedProblemIds[i],
                Position = i + 1
            });
        }

        db.Contests.Add(contest);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = contest.Id }, new { id = contest.Id });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContestRequest request, CancellationToken ct = default)
    {
        var contest = await db.Contests.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
                return BadRequest(new { error = "Name cannot be empty." });

            var clash = await db.Contests.AnyAsync(c => c.Name == name && c.Id != id, ct);
            if (clash)
                return Conflict(new { error = "This contest name already exists." });

            contest.Name = name;
        }

        if (request.IsActive.HasValue)
            contest.IsActive = request.IsActive.Value;

        contest.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = contest.Id });
    }

    /// <summary>Adds a problem to a contest at an optional position (defaults to the end).</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{id:guid}/problems")]
    public async Task<IActionResult> AddProblem(Guid id, [FromBody] AddContestProblemRequest request, CancellationToken ct = default)
    {
        var contest = await db.Contests
            .Include(c => c.ContestProblems)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        var problemExists = await db.Problems.AnyAsync(p => p.Id == request.ProblemId, ct);
        if (!problemExists)
            return BadRequest(new { error = "Problem does not exist." });

        if (contest.ContestProblems.Any(cp => cp.ProblemId == request.ProblemId))
            return Conflict(new { error = "Problem is already in this contest." });

        var ordered = contest.ContestProblems.OrderBy(cp => cp.Position).ToList();

        // Clamp the requested 1-based position into [1, count + 1]; null => append.
        var insertAt = request.Position is int p
            ? Math.Clamp(p, 1, ordered.Count + 1)
            : ordered.Count + 1;

        var link = new ContestProblem
        {
            Id = Guid.NewGuid(),
            ContestId = contest.Id,
            ProblemId = request.ProblemId
        };

        ordered.Insert(insertAt - 1, link);
        Repack(ordered);

        // Add via the DbSet so EF marks the client-keyed row as Added (an INSERT). Adding it
        // only to the tracked navigation makes EF treat the supplied Guid key as an existing
        // row and emit an UPDATE that affects 0 rows. Repack already updated the siblings.
        db.ContestProblems.Add(link);
        contest.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { contestId = contest.Id, problemId = request.ProblemId, position = insertAt });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpDelete("{id:guid}/problems/{problemId:guid}")]
    public async Task<IActionResult> RemoveProblem(Guid id, Guid problemId, CancellationToken ct = default)
    {
        var contest = await db.Contests
            .Include(c => c.ContestProblems)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        var link = contest.ContestProblems.FirstOrDefault(cp => cp.ProblemId == problemId);
        if (link is null)
            return NotFound(new { error = "Problem is not in this contest." });

        db.ContestProblems.Remove(link);
        contest.ContestProblems.Remove(link);

        // Close the gap left behind.
        Repack(contest.ContestProblems.OrderBy(cp => cp.Position).ToList());
        contest.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Reorders a contest's problems. The payload must be a permutation of the current set.</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPut("{id:guid}/problems/order")]
    public async Task<IActionResult> ReorderProblems(Guid id, [FromBody] OrderedIdsRequest request, CancellationToken ct = default)
    {
        var contest = await db.Contests
            .Include(c => c.ContestProblems)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        var current = contest.ContestProblems.Select(cp => cp.ProblemId).ToHashSet();
        var requested = request.OrderedIds.Distinct().ToList();

        if (requested.Count != current.Count || !requested.All(current.Contains))
            return BadRequest(new { error = "Ordered ids must be a permutation of the contest's current problems." });

        var byProblemId = contest.ContestProblems.ToDictionary(cp => cp.ProblemId);
        for (var i = 0; i < requested.Count; i++)
            byProblemId[requested[i]].Position = i + 1;

        contest.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = contest.Id });
    }

    /// <summary>
    /// Standings for a contest: for each active app user, how many of the contest's problems
    /// they have solved. Sorted by solved count desc, then nickname.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/standings")]
    public async Task<ActionResult<ContestStandingsResponse>> GetStandings(Guid id, CancellationToken ct = default)
    {
        var contest = await db.Contests
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Problems = c.ContestProblems
                    .OrderBy(cp => cp.Position)
                    .Select(cp => new ContestStandingProblemDto
                    {
                        ProblemId = cp.ProblemId,
                        Position = cp.Position,
                        Title = cp.Problem.Title,
                        Judge = cp.Problem.Judge
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (contest is null)
            return NotFound(new { error = "Contest not found." });

        var problemIds = contest.Problems.Select(p => p.ProblemId).ToList();

        // Solves for this contest's problems, grouped by user.
        var solvedByUser = (await db.UserProblemStatuses
            .AsNoTracking()
            .Where(s => s.IsSolved && problemIds.Contains(s.ProblemId))
            .Select(s => new { s.UserId, s.ProblemId })
            .ToListAsync(ct))
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProblemId).ToList());

        // Only active users who solved at least one problem of this contest.
        var solverIds = solvedByUser.Keys.ToList();

        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && solverIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Nickname,
                FullName = (u.Names + " " + u.Surnames).Trim(),
                University = u.Organization != null ? u.Organization.ShortName : null
            })
            .ToListAsync(ct);

        var rows = users
            .Select(u =>
            {
                var solvedIds = solvedByUser[u.Id];
                return new ContestStandingRowDto
                {
                    Nickname = u.Nickname,
                    FullName = u.FullName,
                    University = u.University,
                    SolvedCount = solvedIds.Count,
                    SolvedProblemIds = solvedIds
                };
            })
            .OrderByDescending(r => r.SolvedCount)
            .ThenBy(r => r.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new ContestStandingsResponse
        {
            ContestId = contest.Id,
            ContestName = contest.Name,
            Problems = contest.Problems,
            Rows = rows
        });
    }

    /// <summary>Reassigns contiguous 1-based positions following the list's current order.</summary>
    private static void Repack(List<ContestProblem> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Position = i + 1;
    }
}
