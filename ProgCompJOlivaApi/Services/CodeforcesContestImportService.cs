using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CodeforcesClient;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// One-shot startup importer, registered only when the <c>ADDCODEFORCES</c> flag is passed.
/// For each configured gym contest it fetches the problem list from Codeforces, registers the
/// gym in the registry, and inserts any problems not already in the database. Idempotent.
/// </summary>
public class CodeforcesContestImportService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CodeforcesContestImportService> logger) : BackgroundService
{
    // Gym contests requested for the ADDCODEFORCES import.
    private static readonly long[] ContestIds = [567665, 567946, 567947, 568427];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ImportAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ADDCODEFORCES import failed.");
        }
    }

    private async Task ImportAsync(CancellationToken ct)
    {
        var key = configuration["Codeforces:Key"];
        var secret = configuration["Codeforces:Secret"];

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("ADDCODEFORCES: Codeforces:Key/Secret not configured; skipping import.");
            return;
        }

        var client = new CodeforcesClient(key, secret);

        logger.LogInformation("ADDCODEFORCES: importing {Count} gym contest(s).", ContestIds.Length);

        foreach (var contestId in ContestIds)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                // Rate-limited inside the client (>=5s after the previous response).
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

        // Register the gym if it isn't already in the registry.
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
