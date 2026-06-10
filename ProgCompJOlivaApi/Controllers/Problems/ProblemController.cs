using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Common;
using ProgCompJOlivaApi.Controllers.Problems.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Controllers.Problems;

[ApiController]
[Route("api/problem")]
public class ProblemController(AppDbContext db) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>
    /// Fast task search for building contests. Filterable by free text (title / external id /
    /// topic / keyword), judge, topic and difficulty range; paginated and sortable.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProblemListItemDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? judge,
        [FromQuery] string? topic,
        [FromQuery] int? minDifficulty,
        [FromQuery] int? maxDifficulty,
        [FromQuery] bool onlyActive = true,
        [FromQuery] string sort = "title",
        [FromQuery] string sortDir = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Problems.AsNoTracking().AsQueryable();

        if (onlyActive)
            query = query.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(judge))
        {
            var j = judge.Trim();
            query = query.Where(p => p.Judge == j);
        }

        if (minDifficulty.HasValue)
            query = query.Where(p => p.Difficulty >= minDifficulty.Value);

        if (maxDifficulty.HasValue)
            query = query.Where(p => p.Difficulty <= maxDifficulty.Value);

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var t = topic.Trim();
            query = query.Where(p => p.Topics.Any(x => x.Name == t));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Title, pattern) ||
                EF.Functions.ILike(p.ExternalId, pattern) ||
                p.Topics.Any(x => EF.Functions.ILike(x.Name, pattern)) ||
                p.Keywords.Any(k => EF.Functions.ILike(k, pattern)));
        }

        var descending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = sort.ToLowerInvariant() switch
        {
            "difficulty" => descending
                ? query.OrderByDescending(p => p.Difficulty).ThenBy(p => p.Title)
                : query.OrderBy(p => p.Difficulty).ThenBy(p => p.Title),
            "created" => descending
                ? query.OrderByDescending(p => p.CreatedAtUtc)
                : query.OrderBy(p => p.CreatedAtUtc),
            _ => descending
                ? query.OrderByDescending(p => p.Title)
                : query.OrderBy(p => p.Title),
        };

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProblemListItemDto
            {
                Id = p.Id,
                Judge = p.Judge,
                Title = p.Title,
                Url = p.Url,
                ExternalId = p.ExternalId,
                Difficulty = p.Difficulty,
                Topics = p.Topics.Select(t => t.Name).OrderBy(n => n).ToList(),
                Keywords = p.Keywords,
                HasStatement = p.StatementPath != null && p.StatementPath != "",
                IsActive = p.IsActive
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<ProblemListItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        });
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProblemDetailDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var problem = await db.Problems
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProblemDetailDto
            {
                Id = p.Id,
                Judge = p.Judge,
                Title = p.Title,
                Url = p.Url,
                ExternalId = p.ExternalId,
                ContestId = p.ContestId,
                ContestProblemId = p.ContestProblemId,
                Difficulty = p.Difficulty,
                TagsJson = p.TagsJson,
                StatementPath = p.StatementPath,
                Topics = p.Topics.Select(t => t.Name).OrderBy(n => n).ToList(),
                Keywords = p.Keywords,
                IsActive = p.IsActive,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (problem is null)
            return NotFound(new { error = "Problem not found." });

        return Ok(problem);
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("codeforces")]
    public async Task<IActionResult> CreateCodeforces([FromBody] CreateCodeforcesProblemRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        var contestProblemId = request.ContestProblemId.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "Title is required." });

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "Url is required." });

        if (string.IsNullOrWhiteSpace(contestProblemId))
            return BadRequest(new { error = "Contest Problem Id is required." });

        if (request.ContestId < 0)
            return BadRequest(new { error = "Contest Id should be a non negative integer." });

        var problemExists = await db.Problems
            .AnyAsync(x => (x.Title == title && x.Judge == Judges.Codeforces && (x.ContestId ?? -1) == request.ContestId && (x.ContestProblemId ?? "") == contestProblemId) || x.Url == url, ct);

        if (problemExists)
            return Conflict(new { error = "This problem already exists." });

        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Judge = Judges.Codeforces,
            ContestId = request.ContestId,
            ContestProblemId = contestProblemId,
            ExternalId = $"{request.ContestId}/problem/{contestProblemId}",
            Title = title,
            Url = url,
            Difficulty = request.Difficulty,
            TagsJson = request.TagsJson,
            StatementPath = NormalizeOptional(request.StatementPath),
            Keywords = NormalizeKeywords(request.Keywords),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await AssignTopicsAsync(problem, request.Topics, ct);

        db.Problems.Add(problem);

        // Auto-register the gym this task comes from, so the gym list stays in sync with tasks.
        await EnsureGymRegisteredAsync(request.ContestId, ct);

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = problem.Id }, new { id = problem.Id });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("atcoder")]
    public async Task<IActionResult> CreateAtcoder([FromBody] CreateAtcoderProblemRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        var contestId = request.ContestId.Trim();
        var taskId = request.TaskId.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "Title is required." });

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "Url is required." });

        if (string.IsNullOrWhiteSpace(contestId))
            return BadRequest(new { error = "Contest id (e.g. abc300) is required." });

        if (string.IsNullOrWhiteSpace(taskId))
            return BadRequest(new { error = "Task id (e.g. abc300_a) is required." });

        var problemExists = await db.Problems
            .AnyAsync(x => (x.Judge == Judges.AtCoder && x.ExternalId == taskId) || x.Url == url, ct);

        if (problemExists)
            return Conflict(new { error = "This problem already exists." });

        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Judge = Judges.AtCoder,
            ContestId = null,
            ContestProblemId = contestId,
            ExternalId = taskId,
            Title = title,
            Url = url,
            Difficulty = request.Difficulty,
            TagsJson = request.TagsJson,
            StatementPath = NormalizeOptional(request.StatementPath),
            Keywords = NormalizeKeywords(request.Keywords),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await AssignTopicsAsync(problem, request.Topics, ct);

        db.Problems.Add(problem);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = problem.Id }, new { id = problem.Id });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("cses")]
    public async Task<IActionResult> CreateCses([FromBody] CreateCsesProblemRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();
        var url = request.Url.Trim();
        var csesId = request.CsesId.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "Title is required." });

        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new { error = "Url is required." });

        if (string.IsNullOrWhiteSpace(csesId))
            return BadRequest(new { error = "CSES id is required." });

        var problemExists = await db.Problems
            .AnyAsync(x => (x.Judge == Judges.Cses && x.ExternalId == csesId) || x.Url == url, ct);

        if (problemExists)
            return Conflict(new { error = "This problem already exists." });

        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Judge = Judges.Cses,
            ContestId = null,
            ContestProblemId = null,
            ExternalId = csesId,
            Title = title,
            Url = url,
            Difficulty = request.Difficulty,
            TagsJson = request.TagsJson,
            StatementPath = NormalizeOptional(request.StatementPath),
            Keywords = NormalizeKeywords(request.Keywords),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await AssignTopicsAsync(problem, request.Topics, ct);

        db.Problems.Add(problem);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = problem.Id }, new { id = problem.Id });
    }

    [Authorize(Roles = Constants.AdminRole)]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProblemRequest request, CancellationToken ct = default)
    {
        var problem = await db.Problems
            .Include(p => p.Topics)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (problem is null)
            return NotFound(new { error = "Problem not found." });

        if (request.Title != null)
        {
            var title = request.Title.Trim();
            if (title.Length == 0)
                return BadRequest(new { error = "Title cannot be empty." });
            problem.Title = title;
        }

        if (request.Url != null)
        {
            var url = request.Url.Trim();
            if (url.Length == 0)
                return BadRequest(new { error = "Url cannot be empty." });
            problem.Url = url;
        }

        if (request.Difficulty.HasValue)
            problem.Difficulty = request.Difficulty.Value;

        if (request.IsActive.HasValue)
            problem.IsActive = request.IsActive.Value;

        if (request.TagsJson != null)
            problem.TagsJson = request.TagsJson;

        if (request.StatementPath != null)
            problem.StatementPath = NormalizeOptional(request.StatementPath);

        if (request.Keywords != null)
            problem.Keywords = NormalizeKeywords(request.Keywords);

        if (request.Topics != null)
            await AssignTopicsAsync(problem, request.Topics, ct);

        problem.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { id = problem.Id });
    }

    /// <summary>
    /// Records whether a problem is solved. Defaults to the caller; an Admin may target
    /// another user via <c>UserNickname</c>. Upserts the per-(user, problem) status row.
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}/solved")]
    public async Task<IActionResult> SetSolved(Guid id, [FromBody] SetProblemSolvedRequest request, CancellationToken ct = default)
    {
        var problemExists = await db.Problems.AnyAsync(p => p.Id == id, ct);
        if (!problemExists)
            return NotFound(new { error = "Problem not found." });

        var callerNickname = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(callerNickname))
            return Unauthorized();

        var targetNickname = callerNickname;

        if (!string.IsNullOrWhiteSpace(request.UserNickname)
            && !string.Equals(request.UserNickname.Trim(), callerNickname, StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole(Constants.AdminRole))
                return Forbid();

            targetNickname = request.UserNickname.Trim();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Nickname == targetNickname, ct);
        if (user is null)
            return NotFound(new { error = "Target user not found." });

        var status = await db.UserProblemStatuses
            .FirstOrDefaultAsync(s => s.UserId == user.Id && s.ProblemId == id, ct);

        var now = DateTime.UtcNow;

        if (status is null)
        {
            status = new UserProblemStatus
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ProblemId = id,
                CreatedAtUtc = now
            };
            db.UserProblemStatuses.Add(status);
        }

        status.IsSolved = request.IsSolved;
        status.SolvedAtUtc = request.IsSolved ? (status.SolvedAtUtc ?? now) : null;
        status.LastCheckedAtUtc = now;
        if (request.Notes != null)
            status.Notes = request.Notes;
        status.UpdatedAtUtc = now;

        await db.SaveChangesAsync(ct);

        return Ok(new { problemId = id, userNickname = targetNickname, isSolved = status.IsSolved });
    }

    /// <summary>
    /// Registers a Codeforces gym (by its contest id) with the default <see cref="GymFetchMethod.Standings"/>
    /// fetch method if it isn't already in the registry. No-ops for non-positive ids or gyms that
    /// already exist. The new row is saved together with the problem by the caller.
    /// </summary>
    private async Task EnsureGymRegisteredAsync(int gymContestId, CancellationToken ct)
    {
        if (gymContestId <= 0)
            return;

        var alreadyRegistered = await db.CodeforcesGyms.AnyAsync(g => g.GymContestId == gymContestId, ct);
        if (alreadyRegistered)
            return;

        var now = DateTime.UtcNow;
        db.CodeforcesGyms.Add(new CodeforcesGym
        {
            Id = Guid.NewGuid(),
            GymContestId = gymContestId,
            FetchMethod = GymFetchMethod.Standings,
            Enabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> NormalizeKeywords(List<string> keywords)
        => keywords
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Replaces a problem's topics with the given names, creating any topic that doesn't
    /// exist yet (case-insensitive match on name). The problem's <c>Topics</c> collection
    /// must already be loaded for updates.
    /// </summary>
    private async Task AssignTopicsAsync(Problem problem, List<string> topicNames, CancellationToken ct)
    {
        var normalized = topicNames
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        problem.Topics.Clear();

        if (normalized.Count == 0)
            return;

        var lowered = normalized.Select(n => n.ToLowerInvariant()).ToList();

        var existing = await db.Topics
            .Where(t => lowered.Contains(t.Name.ToLower()))
            .ToListAsync(ct);

        foreach (var name in normalized)
        {
            var topic = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

            if (topic is null)
            {
                topic = new Topic { Id = Guid.NewGuid(), Name = name };
                db.Topics.Add(topic);
                existing.Add(topic);
            }

            problem.Topics.Add(topic);
        }
    }
}
