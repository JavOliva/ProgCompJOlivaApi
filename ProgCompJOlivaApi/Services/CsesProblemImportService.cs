using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CsesClient;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Runs once at startup: fetches the public CSES problemset list and inserts any tasks that
/// aren't in the database yet (matched by <c>Problem.ExternalId</c> among CSES problems).
/// Existing problems are left untouched. Failures (e.g. CSES unreachable) are logged and
/// swallowed so they never block or crash API startup.
/// </summary>
public class CsesProblemImportService(IServiceScopeFactory scopeFactory, ILogger<CsesProblemImportService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ImportAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            // Never let a startup import failure take the host down.
            logger.LogError(ex, "CSES problem import failed.");
        }
    }

    private async Task ImportAsync(CancellationToken ct)
    {
        List<CsesProblemInfo> problems;
        try
        {
            problems = await CsesProblemsetScraper.GetAllProblemsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CSES problem import: could not fetch the problemset list; skipping this run.");
            return;
        }

        if (problems.Count == 0)
        {
            logger.LogWarning("CSES problem import: parsed 0 problems from the list page; skipping.");
            return;
        }

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
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToList();

        if (toAdd.Count == 0)
        {
            logger.LogInformation("CSES problem import: all {Total} problems already present.", problems.Count);
            return;
        }

        db.Problems.AddRange(toAdd);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "CSES problem import: added {Added} new problem(s) ({Total} listed on CSES).",
            toAdd.Count, problems.Count);
    }
}
