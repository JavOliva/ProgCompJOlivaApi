using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CodeforcesClient;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Periodically (every 5 minutes after the previous run finishes) scrapes the standings of every
/// enabled Codeforces gym in the registry and marks problems solved for users by their Codeforces
/// handle. Only sets solved = true; it never un-solves. Codeforces calls are throttled globally
/// (>=5s between calls) inside <see cref="CodeforcesClient"/>.
/// </summary>
public class CodeforcesSolveSyncService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CodeforcesSolveSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (and any ADDCODEFORCES import) settle before the first run.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Codeforces solve sync: cycle failed.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        var key = configuration["Codeforces:Key"];
        var secret = configuration["Codeforces:Secret"];

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Codeforces solve sync: Key/Secret not configured; skipping.");
            return;
        }

        var client = new CodeforcesClient(key, secret);

        List<int> gymIds;
        Dictionary<string, Guid> handleToUserId;
        List<string> handles;

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            gymIds = await db.CodeforcesGyms
                .Where(g => g.Enabled && g.FetchMethod == GymFetchMethod.Standings)
                .Select(g => g.GymContestId)
                .ToListAsync(ct);

            var userHandles = await db.Users
                .Where(u => u.CodeforcesHandle != null && u.IsActive)
                .Select(u => new { u.Id, Handle = u.CodeforcesHandle! })
                .ToListAsync(ct);

            handleToUserId = userHandles
                .Where(u => !string.IsNullOrWhiteSpace(u.Handle))
                .GroupBy(u => u.Handle.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);

            handles = handleToUserId.Keys.ToList();
        }

        if (gymIds.Count == 0 || handles.Count == 0)
            return;

        foreach (var gymId in gymIds)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var standings = await client.GetContestStandingsAsync(gymId, handles, showUnofficial: true, ct);
                var changed = await ApplySolvesAsync(gymId, standings, handleToUserId, ct);
                logger.LogInformation("Codeforces solve sync: gym {Gym}: {Changed} solve(s) updated.", gymId, changed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Codeforces solve sync: failed for gym {Gym}.", gymId);
            }
        }
    }

    private async Task<int> ApplySolvesAsync(
        int gymId,
        CodeforcesStandings standings,
        Dictionary<string, Guid> handleToUserId,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Map this gym's problems: Codeforces index ("A") -> our Problem id.
        var ourProblems = await db.Problems
            .Where(p => p.Judge == Judges.Codeforces && p.ContestId == gymId && p.ContestProblemId != null)
            .Select(p => new { p.Id, Index = p.ContestProblemId! })
            .ToListAsync(ct);

        if (ourProblems.Count == 0)
            return 0;

        var indexToProblemId = ourProblems.ToDictionary(p => p.Index, p => p.Id);
        var problemIds = ourProblems.Select(p => p.Id).ToHashSet();

        // Collect (userId, problemId) pairs that the standings say are solved.
        var solvedPairs = new HashSet<(Guid UserId, Guid ProblemId)>();

        foreach (var row in standings.Rows)
        {
            var count = Math.Min(standings.Problems.Count, row.ProblemResults.Count);

            foreach (var member in row.Party.Members)
            {
                if (!handleToUserId.TryGetValue(member.Handle.Trim().ToLowerInvariant(), out var userId))
                    continue;

                for (var i = 0; i < count; i++)
                {
                    if (!row.ProblemResults[i].Solved)
                        continue;

                    if (indexToProblemId.TryGetValue(standings.Problems[i].Index, out var problemId))
                        solvedPairs.Add((userId, problemId));
                }
            }
        }

        if (solvedPairs.Count == 0)
            return 0;

        var existing = (await db.UserProblemStatuses
            .Where(s => problemIds.Contains(s.ProblemId))
            .ToListAsync(ct))
            .ToDictionary(s => (s.UserId, s.ProblemId));

        var now = DateTime.UtcNow;
        var changed = 0;

        foreach (var (userId, problemId) in solvedPairs)
        {
            if (existing.TryGetValue((userId, problemId), out var status))
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
