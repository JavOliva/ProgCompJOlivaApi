using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Common;
using ProgCompJOlivaApi.Controllers.Trainings.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Controllers.Trainings;

[ApiController]
[Route("api/training")]
public class TrainingController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>Search trainings by name, paginated.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<PagedResult<TrainingListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] bool? onlyActive,
        [FromQuery] bool? onlyPublic,
        [FromQuery] string sortDir = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Trainings.AsNoTracking().AsQueryable();

        if (onlyActive == true)
            query = query.Where(t => t.IsActive);

        if (onlyPublic == true)
            query = query.Where(t => t.IsPublic);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(t => EF.Functions.ILike(t.Name, pattern));
        }

        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = descending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TrainingListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Description = t.Description,
                IsPublic = t.IsPublic,
                IsActive = t.IsActive,
                ContestCount = t.TrainingContests.Count,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<TrainingListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var training = await db.Trainings
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TrainingDetailDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                Description = t.Description,
                IsPublic = t.IsPublic,
                IsActive = t.IsActive,
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
                Contests = t.TrainingContests
                    .OrderBy(tc => tc.Position)
                    .Select(tc => new TrainingContestDto
                    {
                        Position = tc.Position,
                        ContestId = tc.ContestId,
                        Name = tc.Contest.Name,
                        IsActive = tc.Contest.IsActive,
                        ProblemCount = tc.Contest.ContestProblems.Count
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (training is null)
            return NotFound(new { error = "Training not found." });

        return Ok(training);
    }

    /// <summary>Creates a training, optionally from an ordered list of contests.</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTrainingRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Name is required." });

        var nameExists = await db.Trainings.AnyAsync(x => x.Name == name, ct);
        if (nameExists)
            return Conflict(new { error = "This training name already exists." });

        var orderedContestIds = request.ContestIds.Distinct().ToList();

        if (orderedContestIds.Count > 0)
        {
            var found = await db.Contests
                .Where(c => orderedContestIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(ct);

            var missing = orderedContestIds.Except(found).ToList();
            if (missing.Count > 0)
                return BadRequest(new { error = "Some contests do not exist.", missing });
        }

        var now = DateTime.UtcNow;
        var training = new Training
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = await GenerateUniqueSlugAsync(name, ct),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            IsPublic = request.IsPublic,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        for (var i = 0; i < orderedContestIds.Count; i++)
        {
            training.TrainingContests.Add(new TrainingContest
            {
                Id = Guid.NewGuid(),
                TrainingId = training.Id,
                ContestId = orderedContestIds[i],
                Position = i + 1
            });
        }

        db.Trainings.Add(training);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = training.Id }, new { id = training.Id });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTrainingRequest request, CancellationToken ct = default)
    {
        var training = await db.Trainings.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (training is null)
            return NotFound(new { error = "Training not found." });

        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
                return BadRequest(new { error = "Name cannot be empty." });

            var clash = await db.Trainings.AnyAsync(t => t.Name == name && t.Id != id, ct);
            if (clash)
                return Conflict(new { error = "This training name already exists." });

            training.Name = name;
        }

        if (request.Description != null)
            training.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.IsPublic.HasValue)
            training.IsPublic = request.IsPublic.Value;

        if (request.IsActive.HasValue)
            training.IsActive = request.IsActive.Value;

        training.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = training.Id });
    }

    /// <summary>Adds a contest to a training at an optional position (defaults to the end).</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{id:guid}/contests")]
    public async Task<IActionResult> AddContest(Guid id, [FromBody] AddTrainingContestRequest request, CancellationToken ct = default)
    {
        var training = await db.Trainings
            .Include(t => t.TrainingContests)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (training is null)
            return NotFound(new { error = "Training not found." });

        var contestExists = await db.Contests.AnyAsync(c => c.Id == request.ContestId, ct);
        if (!contestExists)
            return BadRequest(new { error = "Contest does not exist." });

        if (training.TrainingContests.Any(tc => tc.ContestId == request.ContestId))
            return Conflict(new { error = "Contest is already in this training." });

        var ordered = training.TrainingContests.OrderBy(tc => tc.Position).ToList();

        var insertAt = request.Position is int p
            ? Math.Clamp(p, 1, ordered.Count + 1)
            : ordered.Count + 1;

        var link = new TrainingContest
        {
            Id = Guid.NewGuid(),
            TrainingId = training.Id,
            ContestId = request.ContestId
        };

        ordered.Insert(insertAt - 1, link);
        Repack(ordered);

        // Add via the DbSet so EF marks the client-keyed row as Added (an INSERT). Adding it
        // only to the tracked navigation makes EF treat the supplied Guid key as an existing
        // row and emit an UPDATE that affects 0 rows. Repack already updated the siblings.
        db.TrainingContests.Add(link);
        training.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { trainingId = training.Id, contestId = request.ContestId, position = insertAt });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpDelete("{id:guid}/contests/{contestId:guid}")]
    public async Task<IActionResult> RemoveContest(Guid id, Guid contestId, CancellationToken ct = default)
    {
        var training = await db.Trainings
            .Include(t => t.TrainingContests)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (training is null)
            return NotFound(new { error = "Training not found." });

        var link = training.TrainingContests.FirstOrDefault(tc => tc.ContestId == contestId);
        if (link is null)
            return NotFound(new { error = "Contest is not in this training." });

        db.TrainingContests.Remove(link);
        training.TrainingContests.Remove(link);

        Repack(training.TrainingContests.OrderBy(tc => tc.Position).ToList());
        training.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Reorders a training's contests. The payload must be a permutation of the current set.</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPut("{id:guid}/contests/order")]
    public async Task<IActionResult> ReorderContests(Guid id, [FromBody] OrderedIdsRequest request, CancellationToken ct = default)
    {
        var training = await db.Trainings
            .Include(t => t.TrainingContests)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (training is null)
            return NotFound(new { error = "Training not found." });

        var current = training.TrainingContests.Select(tc => tc.ContestId).ToHashSet();
        var requested = request.OrderedIds.Distinct().ToList();

        if (requested.Count != current.Count || !requested.All(current.Contains))
            return BadRequest(new { error = "Ordered ids must be a permutation of the training's current contests." });

        var byContestId = training.TrainingContests.ToDictionary(tc => tc.ContestId);
        for (var i = 0; i < requested.Count; i++)
            byContestId[requested[i]].Position = i + 1;

        training.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = training.Id });
    }

    /// <summary>
    /// Global training standings: for each active app user, how many problems they solved in
    /// each contest of the training, plus the overall total. Sorted by total desc, then nickname.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/standings")]
    public async Task<ActionResult<TrainingStandingsResponse>> GetStandings(Guid id, CancellationToken ct = default)
    {
        var training = await db.Trainings
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                Contests = t.TrainingContests
                    .OrderBy(tc => tc.Position)
                    .Select(tc => new { tc.ContestId, tc.Contest.Name, tc.Position })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        if (training is null)
            return NotFound(new { error = "Training not found." });

        var contestIds = training.Contests.Select(c => c.ContestId).ToList();

        // contestId -> set of its problem ids
        var contestProblemPairs = await db.ContestProblems
            .AsNoTracking()
            .Where(cp => contestIds.Contains(cp.ContestId))
            .Select(cp => new { cp.ContestId, cp.ProblemId })
            .ToListAsync(ct);

        var problemsByContest = contestIds.ToDictionary(
            cid => cid,
            cid => contestProblemPairs.Where(p => p.ContestId == cid).Select(p => p.ProblemId).ToHashSet());

        var allProblemIds = contestProblemPairs.Select(p => p.ProblemId).Distinct().ToList();

        // user -> set of solved problem ids (within this training)
        var solvedPairs = await db.UserProblemStatuses
            .AsNoTracking()
            .Where(s => s.IsSolved && allProblemIds.Contains(s.ProblemId))
            .Select(s => new { s.UserId, s.ProblemId })
            .ToListAsync(ct);

        var solvedByUser = solvedPairs
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProblemId).ToHashSet());

        var users = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
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
                var solved = solvedByUser.TryGetValue(u.Id, out var ids) ? ids : [];

                var perContest = training.Contests
                    .Select(c => new TrainingStandingCellDto
                    {
                        ContestId = c.ContestId,
                        Solved = problemsByContest.TryGetValue(c.ContestId, out var probs)
                            ? solved.Count(probs.Contains)
                            : 0
                    })
                    .ToList();

                return new TrainingStandingRowDto
                {
                    Nickname = u.Nickname,
                    FullName = u.FullName,
                    University = u.University,
                    PerContest = perContest,
                    Total = perContest.Sum(c => c.Solved)
                };
            })
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Nickname, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(new TrainingStandingsResponse
        {
            TrainingId = training.Id,
            TrainingName = training.Name,
            Contests = training.Contests
                .Select(c => new TrainingStandingContestDto
                {
                    ContestId = c.ContestId,
                    Name = c.Name,
                    Position = c.Position,
                    ProblemCount = problemsByContest.TryGetValue(c.ContestId, out var probs) ? probs.Count : 0
                })
                .ToList(),
            Rows = rows
        });
    }

    private static void Repack(List<TrainingContest> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Position = i + 1;
    }

    /// <summary>Builds a unique slug from the name, suffixing <c>-2</c>, <c>-3</c>… on collisions.</summary>
    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.Generate(name);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "training";

        var slug = baseSlug;
        var suffix = 2;

        while (await db.Trainings.AnyAsync(t => t.Slug == slug, ct))
            slug = $"{baseSlug}-{suffix++}";

        return slug;
    }
}
