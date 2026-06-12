using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.IcpcTraining;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// On startup, turns every seed contest file in <c>SeedData/standings/*.dat</c> into stored ICPC
/// standings JSON (under <c>wwwroot/standings/</c>, keyed by file name), so they're available from
/// the standings endpoint without needing to upload them. Idempotent: a key whose JSON already
/// exists is left untouched (so re-runs and uploads are never clobbered). Delete a key's JSON to
/// re-seed it from its <c>.dat</c>.
/// </summary>
public class IcpcStandingsSeedService(IWebHostEnvironment env, ILogger<IcpcStandingsSeedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData", "standings");
            if (!Directory.Exists(seedDir))
                return;

            var seeded = 0;
            foreach (var datPath in Directory.GetFiles(seedDir, "*.dat"))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var key = Path.GetFileNameWithoutExtension(datPath);

                    // Idempotent: if these standings were already seeded (or uploaded), do nothing.
                    if (IcpcStandingsStore.Exists(env, key))
                        continue;

                    var bytes = await File.ReadAllBytesAsync(datPath, stoppingToken);
                    var standings = DatStandingsParser.Parse(bytes);

                    // Derive org/year/fase from the file name (e.g. usm2024-1 -> usm/2024/1).
                    if (IcpcStandingsKey.TryParse(key, out var org, out var year, out var fase))
                    {
                        standings.Org = org;
                        standings.Year = year;
                        standings.Fase = fase;
                    }

                    await IcpcStandingsStore.SaveAsync(env, key, standings, stoppingToken);
                    seeded++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "ICPC standings seed: failed to process {File}.", datPath);
                }
            }

            if (seeded > 0)
                logger.LogInformation("ICPC standings seed: generated {Count} standings from seed .dat file(s).", seeded);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ICPC standings seed failed.");
        }
    }
}
