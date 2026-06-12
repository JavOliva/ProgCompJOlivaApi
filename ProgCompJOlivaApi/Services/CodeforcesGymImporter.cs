using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CodeforcesClient;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Imports a Codeforces gym: registers it in the gym registry (enabled, so the solve sync starts
/// fetching its standings) and inserts its problems via signed <c>contest.standings</c>. Problems
/// are de-duplicated by (gym, index) — re-importing the same gym is idempotent. All Codeforces
/// calls go through <see cref="CodeforcesClient"/>'s process-wide ≥5s gate.
/// </summary>
public class CodeforcesGymImporter(AppDbContext db, IConfiguration configuration, IWebHostEnvironment env)
{
    public record Result(int GymContestId, string? Name, int AddedProblems, int TotalProblems, bool GymWasNew);

    public async Task<Result> ImportAsync(int gymContestId, CancellationToken ct)
    {
        if (gymContestId <= 0)
            throw new ArgumentException("Gym contest id must be a positive integer.", nameof(gymContestId));

        var key = configuration["Codeforces:Key"];
        var secret = configuration["Codeforces:Secret"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Codeforces:Key/Secret are not configured.");

        var client = new CodeforcesClient(key, secret);

        // One rate-limited Codeforces call: gives the gym name + its problem list.
        var standings = await client.GetContestStandingsAsync(gymContestId, handles: null, showUnofficial: false, ct);

        var now = DateTime.UtcNow;

        // Register the gym (or make sure it's enabled for the standings sync).
        var gym = await db.CodeforcesGyms.FirstOrDefaultAsync(g => g.GymContestId == gymContestId, ct);
        var gymWasNew = gym is null;

        if (gym is null)
        {
            db.CodeforcesGyms.Add(new CodeforcesGym
            {
                Id = Guid.NewGuid(),
                GymContestId = gymContestId,
                Name = standings.Contest?.Name,
                FetchMethod = GymFetchMethod.Standings,
                Enabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            if (!gym.Enabled)
                gym.Enabled = true;
            if (string.IsNullOrWhiteSpace(gym.Name) && !string.IsNullOrWhiteSpace(standings.Contest?.Name))
                gym.Name = standings.Contest!.Name;
            gym.UpdatedAtUtc = now;
        }

        var existingIndexes = (await db.Problems
            .Where(p => p.Judge == Judges.Codeforces && p.ContestId == gymContestId)
            .Select(p => p.ContestProblemId)
            .ToListAsync(ct))
            .Where(x => x != null)
            .ToHashSet();

        var added = 0;
        foreach (var problem in standings.Problems)
        {
            if (existingIndexes.Contains(problem.Index))
                continue;

            var externalId = $"{gymContestId}/problem/{problem.Index}";
            db.Problems.Add(new Problem
            {
                Id = Guid.NewGuid(),
                Judge = Judges.Codeforces,
                ContestId = gymContestId,
                ContestProblemId = problem.Index,
                ExternalId = externalId,
                Title = problem.Name,
                Url = $"https://codeforces.com/gym/{gymContestId}/problem/{problem.Index}",
                Difficulty = problem.Rating,
                TagsJson = problem.Tags.Count > 0 ? JsonSerializer.Serialize(problem.Tags) : null,
                StatementPath = StatementStore.EnsureFolder(env, Judges.Codeforces, externalId),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            added++;
        }

        await db.SaveChangesAsync(ct);

        return new Result(gymContestId, standings.Contest?.Name, added, standings.Problems.Count, gymWasNew);
    }
}
