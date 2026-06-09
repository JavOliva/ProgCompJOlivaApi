using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.JudgeClients.AtcoderClient;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// Refreshes AtCoder ratings every minute. Codeforces lives in <see cref="CodeforcesWorker"/>
/// (separate site/IP, separate rate budget).
/// </summary>
public class PeriodicWorker(IServiceScopeFactory scopeFactory, ILogger<PeriodicWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PeriodicWorker started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var atcoder = new AtcoderClient();

                var users = await db.Users
                    .Where(u => u.AtcoderHandle != null)
                    .ToListAsync(stoppingToken);

                var handles = users
                    .Select(u => u.AtcoderHandle!)
                    .ToList();

                if (handles.Count == 0)
                    continue;

                var atcoderUsers = await atcoder.GetUsersRatings(handles, ct: stoppingToken);

                foreach (var user in users)
                {
                    if (!atcoderUsers.TryGetValue(user.AtcoderHandle!, out var atcoderRating))
                        continue;

                    user.AtcoderRating = atcoderRating;
                }

                await db.SaveChangesAsync(stoppingToken);

                logger.LogInformation("Periodic job finished for Atcoder Ratings.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in periodic job when fetching Atcoder Ratings.");
            }
        }
    }
}
