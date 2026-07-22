using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.Oci;
using ProgCompJOlivaApi.Controllers.Oci.Dtos;

namespace ProgCompJOlivaApi.Services;

/// <summary>
/// On startup, loads every OCI standings seed in <c>SeedData/oci-standings/</c> into stored
/// standings (under <c>wwwroot/oci-standings/</c>, keyed by file name): <c>*.json</c> as
/// ready-made standings, and <c>*.csv</c> through the same parser as the admin upload
/// (an optional <c>oci</c> filename prefix is accepted, so <c>ocinacional2020.csv</c> seeds the
/// key <c>nacional2020</c>; a <c>-N</c> suffix marks a clasificatoria phase). Idempotent: a key
/// whose JSON already exists is left untouched. Delete a key's JSON to re-seed it from
/// <c>SeedData</c>.
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

            foreach (var csvPath in Directory.GetFiles(seedDir, "*.csv"))
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    if (await SeedCsvAsync(csvPath, stoppingToken))
                        seeded++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "OCI standings seed: failed to process {File}.", csvPath);
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

    /// <summary>Seeds one CSV file; returns true when it produced a new stored standings.</summary>
    private async Task<bool> SeedCsvAsync(string csvPath, CancellationToken ct)
    {
        var key = Path.GetFileNameWithoutExtension(csvPath).Trim().ToLowerInvariant();

        // Accept an "oci" filename prefix: ocinacional2020 -> nacional2020.
        if (!OciStandingsKey.TryParse(key, out var type, out var year))
        {
            if (key.StartsWith("oci") && OciStandingsKey.TryParse(key[3..], out type, out year))
            {
                key = key[3..];
            }
            else
            {
                logger.LogWarning(
                    "OCI standings seed: '{File}' doesn't match {{type}}{{year}}[-{{fase}}] (with optional 'oci' prefix); skipping.",
                    Path.GetFileName(csvPath));
                return false;
            }
        }

        if (OciStandingsStore.Exists(env, key))
            return false;

        // A "-N" key suffix marks a clasificatoria phase (clasificatoria2023-1).
        int? phase = null;
        var dash = key.LastIndexOf('-');
        if (dash > 0 && int.TryParse(key[(dash + 1)..], out var parsedPhase) && parsedPhase > 0)
            phase = parsedPhase;

        var content = await File.ReadAllTextAsync(csvPath, ct);

        var standings = OciCsvParser.Parse(
            content, type, year,
            OciCsvParser.DefaultContestName(type, year, phase, weighted: false));

        if (standings.Rows.Count == 0)
        {
            logger.LogWarning("OCI standings seed: '{File}' has no data rows; skipping.", Path.GetFileName(csvPath));
            return false;
        }

        standings.Phase = phase;

        await OciStandingsStore.SaveAsync(env, key, standings, ct);
        return true;
    }
}
