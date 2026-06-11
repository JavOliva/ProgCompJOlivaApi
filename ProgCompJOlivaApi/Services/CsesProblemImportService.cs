using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CsesClient;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Runs once at startup. First inserts any CSES problems missing from the database (scraped from
/// the public list), then backfills statements: for every CSES problem without a stored statement
/// it fetches the task page, stores the statement HTML, and records its <c>StatementPath</c>.
/// Everything is idempotent and failures are logged and swallowed so they never block startup.
/// </summary>
public class CsesProblemImportService(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment env,
    ILogger<CsesProblemImportService> logger) : BackgroundService
{
    // Polite spacing between CSES page fetches.
    private static readonly TimeSpan StatementDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ImportProblemsAsync(stoppingToken);
            await FetchMissingStatementsAsync(stoppingToken);
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

    private async Task FetchMissingStatementsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pending = await db.Problems
            .Where(p => p.Judge == Judges.Cses && p.StatementPath == null)
            .Select(p => new { p.Id, p.ExternalId })
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        logger.LogInformation("CSES import: fetching {Count} missing statement(s).", pending.Count);

        int fetched = 0, failed = 0;

        foreach (var p in pending)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var html = await CsesStatementScraper.GetStatementHtmlAsync(p.ExternalId, ct);
                if (html is null)
                {
                    failed++;
                }
                else
                {
                    var relative = await StatementStore.SaveAsync(env, Judges.Cses, p.ExternalId, html, ct);
                    await db.Problems
                        .Where(x => x.Id == p.Id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.StatementPath, relative)
                            .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);
                    fetched++;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogDebug(ex, "CSES import: statement fetch failed for task {Task}.", p.ExternalId);
            }

            try { await Task.Delay(StatementDelay, ct); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("CSES import: statements fetched={Fetched}, failed={Failed}.", fetched, failed);
    }
}
