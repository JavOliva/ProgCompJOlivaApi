using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;

namespace ProgCompJOlivaApi.Controllers.IcpcEvents;

/// <summary>
/// Persists imported ICPC-event standings as plain JSON files under
/// <c>wwwroot/icpc-events/{key}.json</c> (key = <c>{event}{year}</c>, e.g. <c>latam2025</c>).
/// </summary>
public static class IcpcEventStandingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static string KeyFor(string @event, int year) => $"{@event.Trim().ToLowerInvariant()}{year}";

    public static string SanitizeKey(string key)
        => new(key.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-').ToArray());

    private static string PathFor(IWebHostEnvironment env, string key)
        => Path.Combine(env.WebRootPath, "icpc-events", $"{SanitizeKey(key)}.json");

    public static bool Exists(IWebHostEnvironment env, string key)
        => File.Exists(PathFor(env, key));

    public static async Task SaveAsync(IWebHostEnvironment env, string key, IcpcEventStandings standings, CancellationToken ct = default)
    {
        var dir = Path.Combine(env.WebRootPath, "icpc-events");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(standings, JsonOptions);
        await File.WriteAllTextAsync(PathFor(env, key), json, ct);
    }

    public static async Task<IcpcEventStandings?> ReadAsync(IWebHostEnvironment env, string key, CancellationToken ct = default)
    {
        var path = PathFor(env, key);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<IcpcEventStandings>(json, JsonOptions);
    }

    public static List<string> ListKeys(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "icpc-events");
        if (!Directory.Exists(dir))
            return [];

        return Directory.GetFiles(dir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(k => !string.IsNullOrEmpty(k))
            .Select(k => k!)
            .OrderBy(k => k)
            .ToList();
    }
}
