using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.CsesClient;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Keeps each user's CSES "rating" up to date. For CSES the rating is simply the number of
/// problems the user has solved: it scrapes the user's solved task ids (by their <c>CsesId</c>,
/// using the service-account cookie) and stores the count in <c>User.CsesRating</c>. Runs every
/// 10 minutes; skipped entirely when no <c>Cses:SessionCookie</c> is configured.
/// </summary>
public class CsesWorker(
    IServiceScopeFactory scopeFactory,
    CsesSolvedScraper scraper,
    IConfiguration configuration,
    ILogger<CsesWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PerUserDelay = TimeSpan.FromSeconds(1);

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
            users = (await db.Users
                .Where(u => u.CsesId != null && u.IsActive)
                .Select(u => new { u.Id, u.CsesId })
                .ToListAsync(ct))
                .Where(u => !string.IsNullOrWhiteSpace(u.CsesId))
                .Select(u => (u.Id, u.CsesId!))
                .ToList();
        }

        if (users.Count == 0)
            return;

        var updated = 0;

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

                updated++;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "CSES rating sync: failed for CSES id {CsesId}.", csesId);
            }

            try { await Task.Delay(PerUserDelay, ct); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("CSES rating sync: updated {Updated}/{Total} user(s).", updated, users.Count);
    }
}
