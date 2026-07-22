using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CsesClient;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Keeps each user's CSES solve state up to date: scrapes the user's statistics page (by their
/// <c>CsesId</c>, using the service-account cookie) and stores both the solved count in
/// <c>User.CsesRating</c> and the per-problem solves in <c>UserProblemStatuses</c> (matching
/// scraped task ids to CSES problems by <c>ExternalId</c>), so trainings and contest standings
/// reflect CSES solves. Waits 10 minutes <b>after finishing a full pass</b> before the next one,
/// pausing a couple of seconds between users to be gentle with cses.fi. Skipped entirely when no
/// <c>Cses:SessionCookie</c> is configured.
/// </summary>
public class CsesWorker(
    IServiceScopeFactory scopeFactory,
    CsesSolvedScraper scraper,
    IConfiguration configuration,
    ILogger<CsesWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PerUserDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(25), ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CSES rating sync failed.");
            }

            try { await Task.Delay(Interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SyncAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configuration["Cses:SessionCookie"]))
        {
            logger.LogWarning("CSES rating sync: Cses:SessionCookie not configured; skipping.");
            return;
        }

        List<(Guid Id, string CsesId)> users;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Sync every user with a CsesId, including inactive ones — CSES solves are historical
            // and a deactivated user's solve state should still be kept up to date.
            users = (await db.Users
                .Where(u => u.CsesId != null)
                .Select(u => new { u.Id, u.CsesId })
                .ToListAsync(ct))
                .Where(u => !string.IsNullOrWhiteSpace(u.CsesId))
                .Select(u => (u.Id, u.CsesId!))
                .ToList();
        }

        if (users.Count == 0)
            return;

        // Scraped task ids -> stored CSES problems (Problem.ExternalId is the task id).
        Dictionary<string, Guid> problemByTaskId;
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            problemByTaskId = await db.Problems
                .AsNoTracking()
                .Where(p => p.Judge == Judges.Cses)
                .Select(p => new { p.ExternalId, p.Id })
                .ToDictionaryAsync(p => p.ExternalId, p => p.Id, ct);
        }

        var updated = 0;
        var newSolves = 0;

        foreach (var (id, csesId) in users)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var solved = await scraper.GetSolvedTaskIdsAsync(csesId, ct);

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Users
                    .Where(u => u.Id == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.CsesRating, solved.Count), ct);

                newSolves += await UpsertSolvedStatusesAsync(db, id, solved, problemByTaskId, ct);

                updated++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CSES sync: failed for CSES id {CsesId}.", csesId);
            }

            try { await Task.Delay(PerUserDelay, ct); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation(
            "CSES sync: updated {Updated}/{Total} user(s), {NewSolves} new solve(s).",
            updated, users.Count, newSolves);
    }

    /// <summary>
    /// Marks the scraped solved tasks as solved in <c>UserProblemStatuses</c> (insert or update;
    /// never un-solves). Returns how many statuses became newly solved.
    /// </summary>
    private static async Task<int> UpsertSolvedStatusesAsync(
        AppDbContext db,
        Guid userId,
        HashSet<string> solvedTaskIds,
        Dictionary<string, Guid> problemByTaskId,
        CancellationToken ct)
    {
        var solvedProblemIds = solvedTaskIds
            .Where(problemByTaskId.ContainsKey)
            .Select(taskId => problemByTaskId[taskId])
            .ToList();

        if (solvedProblemIds.Count == 0)
            return 0;

        var existing = (await db.UserProblemStatuses
            .Where(s => s.UserId == userId && solvedProblemIds.Contains(s.ProblemId))
            .ToListAsync(ct))
            .ToDictionary(s => s.ProblemId);

        var now = DateTime.UtcNow;
        var changed = 0;

        foreach (var problemId in solvedProblemIds)
        {
            if (existing.TryGetValue(problemId, out var status))
            {
                status.LastCheckedAtUtc = now;
                if (!status.IsSolved)
                {
                    status.IsSolved = true;
                    status.SolvedAtUtc ??= now;
                    status.UpdatedAtUtc = now;
                    changed++;
                }
            }
            else
            {
                db.UserProblemStatuses.Add(new UserProblemStatus
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ProblemId = problemId,
                    IsSolved = true,
                    SolvedAtUtc = now,
                    LastCheckedAtUtc = now,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                changed++;
            }
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }
}
