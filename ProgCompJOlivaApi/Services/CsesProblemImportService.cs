using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CsesClient;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Runs once at startup. Inserts any CSES problems missing from the database (scraped from the
/// public list), maintains the built-in <b>CSES training</b> (one contest per problemset category,
/// slug <c>cses</c> — new categories/problems are appended on later runs), then makes sure every
/// problem (any judge) has an empty statement folder reserved (<see cref="StatementStore"/>) with
/// its path recorded in <c>StatementPath</c>. Idempotent; failures are logged and swallowed so
/// they never block startup.
/// </summary>
public class CsesProblemImportService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<CsesProblemImportService> logger) : BackgroundService
{
    /// <summary>Name prefix of the auto-managed per-category contests ("CSES: Sorting and Searching").</summary>
    private const string ContestNamePrefix = "CSES: ";

    /// <summary>Well-known slug of the auto-managed training; the frontend fetches it by this.</summary>
    private const string TrainingSlug = "cses";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            List<CsesProblemInfo> problems = [];
            try
            {
                problems = await CsesProblemsetScraper.GetAllProblemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CSES import: could not fetch the problemset list; skipping problem import.");
            }

            if (problems.Count > 0)
            {
                await ImportProblemsAsync(problems, stoppingToken);
                await EnsureCsesTrainingAsync(problems, stoppingToken);
            }

            await EnsureStatementFoldersAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CSES import failed.");
        }
    }

    private async Task ImportProblemsAsync(List<CsesProblemInfo> problems, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingExternalIds = (await db.Problems
            .Where(p => p.Judge == Judges.Cses)
            .Select(p => p.ExternalId)
            .ToListAsync(ct))
            .ToHashSet();

        var now = DateTime.UtcNow;

        var toAdd = problems
            .Where(p => !existingExternalIds.Contains(p.TaskId))
            .Select(p => new Problem
            {
                Id = Guid.NewGuid(),
                Judge = Judges.Cses,
                ContestId = null,
                ContestProblemId = null,
                ExternalId = p.TaskId,
                Title = p.Title,
                Url = p.Url,
                StatementPath = StatementStore.EnsureFolder(env, Judges.Cses, p.TaskId),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("CSES import: all {Total} problems already present.", problems.Count);
            return;
        }

        db.Problems.AddRange(toAdd);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("CSES import: added {Added} new problem(s) ({Total} listed).", toAdd.Count, problems.Count);
    }

    /// <summary>
    /// Maintains the built-in CSES training: one contest per problemset category (named
    /// "CSES: {category}", problems in page order) and a training (slug <c>cses</c>, not public
    /// so it stays out of the Gimnasio) containing those contests in page order. Append-only and
    /// idempotent: existing positions are never reshuffled; new categories/problems go at the end.
    /// </summary>
    private async Task EnsureCsesTrainingAsync(List<CsesProblemInfo> problems, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Categories in page order, each with its task ids in page order (GroupBy preserves both).
        var categories = problems
            .GroupBy(p => p.Category)
            .Select(g => (Category: g.Key, TaskIds: g.Select(p => p.TaskId).ToList()))
            .ToList();

        var problemIdByTaskId = await db.Problems
            .Where(p => p.Judge == Judges.Cses)
            .Select(p => new { p.ExternalId, p.Id })
            .ToDictionaryAsync(p => p.ExternalId, p => p.Id, ct);

        var contestNames = categories.Select(c => ContestNamePrefix + c.Category).ToList();
        var contestsByName = await db.Contests
            .Include(c => c.ContestProblems)
            .Where(c => contestNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, ct);

        var now = DateTime.UtcNow;
        var newContests = 0;
        var addedProblemLinks = 0;
        var orderedContests = new List<Contest>();

        foreach (var (category, taskIds) in categories)
        {
            var name = ContestNamePrefix + category;

            if (!contestsByName.TryGetValue(name, out var contest))
            {
                contest = new Contest
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                db.Contests.Add(contest);
                contestsByName[name] = contest;
                newContests++;
            }

            orderedContests.Add(contest);

            var present = contest.ContestProblems.Select(cp => cp.ProblemId).ToHashSet();
            var position = contest.ContestProblems.Count > 0
                ? contest.ContestProblems.Max(cp => cp.Position)
                : 0;

            foreach (var taskId in taskIds)
            {
                if (!problemIdByTaskId.TryGetValue(taskId, out var problemId) || present.Contains(problemId))
                    continue;

                // Add via the DbSet so EF treats the client-keyed row as an INSERT (see
                // TrainingController.AddContest for the same gotcha).
                var link = new ContestProblem
                {
                    Id = Guid.NewGuid(),
                    ContestId = contest.Id,
                    ProblemId = problemId,
                    Position = ++position
                };
                db.ContestProblems.Add(link);
                contest.ContestProblems.Add(link);
                contest.UpdatedAtUtc = now;
                addedProblemLinks++;
            }
        }

        var training = await db.Trainings
            .Include(t => t.TrainingContests)
            .FirstOrDefaultAsync(t => t.Slug == TrainingSlug, ct);

        if (training is null)
        {
            training = new Training
            {
                Id = Guid.NewGuid(),
                Name = "CSES",
                Slug = TrainingSlug,
                Description = "El CSES Problem Set completo, una categoría por contest.",
                IsActive = true,
                IsPublic = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Trainings.Add(training);
        }

        var contestsInTraining = training.TrainingContests.Select(tc => tc.ContestId).ToHashSet();
        var trainingPosition = training.TrainingContests.Count > 0
            ? training.TrainingContests.Max(tc => tc.Position)
            : 0;

        foreach (var contest in orderedContests)
        {
            if (contestsInTraining.Contains(contest.Id))
                continue;

            var link = new TrainingContest
            {
                Id = Guid.NewGuid(),
                TrainingId = training.Id,
                ContestId = contest.Id,
                Position = ++trainingPosition
            };
            db.TrainingContests.Add(link);
            training.TrainingContests.Add(link);
            training.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);

        if (newContests > 0 || addedProblemLinks > 0)
        {
            logger.LogInformation(
                "CSES training: {NewContests} new contest(s), {AddedLinks} problem link(s) added.",
                newContests, addedProblemLinks);
        }
    }

    /// <summary>Reserves an empty statement folder for every problem that doesn't have one yet.</summary>
    private async Task EnsureStatementFoldersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.Problems
            .Where(p => p.StatementPath == null)
            .Select(p => new { p.Id, p.Judge, p.ExternalId })
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        foreach (var p in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            var relative = StatementStore.EnsureFolder(env, p.Judge, p.ExternalId);
            await db.Problems
                .Where(x => x.Id == p.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatementPath, relative), ct);
        }

        logger.LogInformation("Statement folders reserved for {Count} problem(s).", pending.Count);
    }
}
