using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.Oci;
using ProgCompJOlivaApi.Controllers.Oci.Dtos;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// On startup, loads every OCI standings seed in <c>SeedData/oci-standings/*.json</c> into stored
/// standings (under <c>wwwroot/oci-standings/</c>, keyed by file name). Idempotent: a key whose JSON
/// already exists is left untouched. Delete a key's JSON to re-seed it from <c>SeedData</c>.
/// </summary>
public class OciStandingsSeedService(IWebHostEnvironment env, ILogger<OciStandingsSeedService> logger)
    : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData", "oci-standings");
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

                    if (OciStandingsStore.Exists(env, key))
                        continue;

                    var content = await File.ReadAllTextAsync(jsonPath, stoppingToken);
                    var standings = JsonSerializer.Deserialize<OciStandings>(content, JsonOptions);
                    if (standings is null)
                        continue;

                    // Derive type/year from the file name (e.g. regional2022 -> regional/2022).
                    if (OciStandingsKey.TryParse(key, out var type, out var year))
                    {
                        standings.Type = type;
                        standings.Year = year;
                    }

                    await OciStandingsStore.SaveAsync(env, key, standings, stoppingToken);
                    seeded++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "OCI standings seed: failed to process {File}.", jsonPath);
                }
            }

            if (seeded > 0)
                logger.LogInformation("OCI standings seed: loaded {Count} standings from seed file(s).", seeded);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCI standings seed failed.");
        }
    }
}
