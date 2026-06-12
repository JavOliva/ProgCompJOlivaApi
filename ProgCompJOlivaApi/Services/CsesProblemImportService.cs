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
/// public list), then makes sure every problem (any judge) has an empty statement folder reserved
/// (<see cref="StatementStore"/>) with its path recorded in <c>StatementPath</c>. Idempotent;
/// failures are logged and swallowed so they never block startup.
/// </summary>
public class CsesProblemImportService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<CsesProblemImportService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ImportProblemsAsync(stoppingToken);
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

    private async Task ImportProblemsAsync(CancellationToken ct)
    {
        List<CsesProblemInfo> problems;
        try
        {
            problems = await CsesProblemsetScraper.GetAllProblemsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CSES import: could not fetch the problemset list; skipping problem import.");
            return;
        }

        if (problems.Count == 0)
        {
            logger.LogWarning("CSES import: parsed 0 problems from the list page; skipping.");
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
