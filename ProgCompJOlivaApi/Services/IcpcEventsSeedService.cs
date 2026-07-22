using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.IcpcEvents;
using ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// On startup, loads every ICPC-event standings seed in <c>SeedData/icpc-events/*.json</c> into
/// stored standings (under <c>wwwroot/icpc-events/</c>, keyed by file name, e.g.
/// <c>latam2025.json</c>). Idempotent: a key whose JSON already exists is left untouched —
/// delete it to re-seed.
/// </summary>
public class IcpcEventsSeedService(IWebHostEnvironment env, ILogger<IcpcEventsSeedService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData", "icpc-events");
            if (!Directory.Exists(seedDir))
                return;

            var seeded = 0;
            foreach (var jsonPath in Directory.GetFiles(seedDir, "*.json"))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var key = Path.GetFileNameWithoutExtension(jsonPath);

                    if (IcpcEventStandingsStore.Exists(env, key))
                        continue;

                    var content = await File.ReadAllTextAsync(jsonPath, stoppingToken);
                    var standings = JsonSerializer.Deserialize<IcpcEventStandings>(content, JsonOptions);
                    if (standings is null)
                        continue;

                    await IcpcEventStandingsStore.SaveAsync(env, key, standings, stoppingToken);
                    seeded++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ICPC events seed: failed to process {File}.", jsonPath);
                }
            }

            if (seeded > 0)
                logger.LogInformation("ICPC events seed: loaded {Count} standings from seed file(s).", seeded);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ICPC events seed failed.");
        }
    }
}
