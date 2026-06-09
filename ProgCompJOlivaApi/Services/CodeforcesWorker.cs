using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CodeforcesClient;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Single owner of all Codeforces API access. Every request leaves from the same server IP, which
/// shares one Codeforces rate-limit budget, so ratings refresh and gym solve-sync run in one
/// coordinated loop instead of as competing background jobs. All CF calls are additionally
/// throttled ≥5s apart inside <see cref="CodeforcesClient"/>. When started with the
/// <c>ADDCODEFORCES</c> flag, the worker first performs a one-shot gym import.
/// </summary>
public class CodeforcesWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CodeforcesWorker> logger,
    bool importOnStartup) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    // Gym contests imported once when the ADDCODEFORCES flag is present.
    private static readonly long[] ImportContestIds = [567665, 567946, 567947, 568427];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Let startup settle before touching the network.
        try { await Task.Delay(TimeSpan.FromSeconds(15), ct); }
        catch (OperationCanceledException) { return; }

        var key = configuration["Codeforces:Key"];
        var secret = configuration["Codeforces:Secret"];
        var hasCreds = !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(secret);

        var client = new CodeforcesClient(key, secret);

        if (importOnStartup)
        {
            try { await ImportGymsAsync(client, hasCreds, ct); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { logger.LogError(ex, "ADDCODEFORCES import failed."); }
        }

        while (!ct.IsCancellationRequested)
        {
            // Ratings (user.info) — works without credentials.
            try { await SyncRatingsAsync(client, ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Codeforces ratings sync failed."); }

            // Gym solves (signed contest.standings) — needs credentials.
            if (hasCreds)
            {
                try { await SyncGymSolvesAsync(client, ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { logger.LogError(ex, "Codeforces solve sync failed."); }
            }

            try { await Task.Delay(Interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ---- Ratings (moved here from PeriodicWorker so CF has a single owner) -----------------

    private async Task SyncRatingsAsync(CodeforcesClient client, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.Users
            .Where(u => u.CodeforcesHandle != null)
            .ToListAsync(ct);

        var handles = users
            .Select(u => u.CodeforcesHandle!)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (handles.Count == 0)
            return;

        var cfUsers = await client.GetUsersInfoAsync(handles, ct: ct);

        var byHandle = new Dictionary<string, CodeforcesUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var cf in cfUsers)
            byHandle[cf.Handle] = cf;

        foreach (var user in users)
        {
            if (user.CodeforcesHandle != null && byHandle.TryGetValue(user.CodeforcesHandle, out var cf))
                user.CodeforcesRating = cf.Rating ?? 0;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Codeforces ratings synced for {Count} handle(s).", handles.Count);
    }

    // ---- Gym solve sync -------------------------------------------------------------------

    private async Task SyncGymSolvesAsync(CodeforcesClient client, CancellationToken ct)
    {
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

        var ourProblems = await db.Problems
            .Where(p => p.Judge == Judges.Codeforces && p.ContestId == gymId && p.ContestProblemId != null)
            .Select(p => new { p.Id, Index = p.ContestProblemId! })
            .ToListAsync(ct);

        if (ourProblems.Count == 0)
            return 0;

        var indexToProblemId = ourProblems.ToDictionary(p => p.Index, p => p.Id);
        var problemIds = ourProblems.Select(p => p.Id).ToHashSet();

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

    // ---- One-shot gym import (ADDCODEFORCES) ----------------------------------------------

    private async Task ImportGymsAsync(CodeforcesClient client, bool hasCreds, CancellationToken ct)
    {
        if (!hasCreds)
        {
            logger.LogWarning("ADDCODEFORCES: Codeforces:Key/Secret not configured; skipping import.");
            return;
        }

        logger.LogInformation("ADDCODEFORCES: importing {Count} gym contest(s).", ImportContestIds.Length);

        foreach (var contestId in ImportContestIds)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var standings = await client.GetContestStandingsAsync(contestId, handles: null, showUnofficial: false, ct);

                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var added = await ImportContestAsync(db, contestId, standings, ct);

                logger.LogInformation(
                    "ADDCODEFORCES: gym {Gym} ({Name}): added {Added} problem(s) of {Total}.",
                    contestId, standings.Contest?.Name ?? "?", added, standings.Problems.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ADDCODEFORCES: failed to import gym {Gym}.", contestId);
            }
        }

        logger.LogInformation("ADDCODEFORCES import finished.");
    }

    private static async Task<int> ImportContestAsync(AppDbContext db, long contestId, CodeforcesStandings standings, CancellationToken ct)
    {
        var gymId = (int)contestId;

        if (!await db.CodeforcesGyms.AnyAsync(g => g.GymContestId == gymId, ct))
        {
            var ts = DateTime.UtcNow;
            db.CodeforcesGyms.Add(new CodeforcesGym
            {
                Id = Guid.NewGuid(),
                GymContestId = gymId,
                Name = standings.Contest?.Name,
                FetchMethod = GymFetchMethod.Standings,
                Enabled = true,
                CreatedAtUtc = ts,
                UpdatedAtUtc = ts
            });
        }

        var existingIndexes = (await db.Problems
            .Where(p => p.Judge == Judges.Codeforces && p.ContestId == gymId)
            .Select(p => p.ContestProblemId)
            .ToListAsync(ct))
            .Where(x => x != null)
            .ToHashSet();

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var problem in standings.Problems)
        {
            if (existingIndexes.Contains(problem.Index))
                continue;

            db.Problems.Add(new Problem
            {
                Id = Guid.NewGuid(),
                Judge = Judges.Codeforces,
                ContestId = gymId,
                ContestProblemId = problem.Index,
                ExternalId = $"{gymId}/problem/{problem.Index}",
                Title = problem.Name,
                Url = $"https://codeforces.com/gym/{gymId}/problem/{problem.Index}",
                Difficulty = problem.Rating,
                TagsJson = problem.Tags.Count > 0 ? JsonSerializer.Serialize(problem.Tags) : null,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }
}
