using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using ProgCompJOlivaApi.Controllers.IcpcTraining.Dtos;

namespace ProgCompJOlivaApi.Controllers.IcpcTraining;

/// <summary>
/// Persists computed ICPC standings as plain JSON files under <c>wwwroot/standings/{key}.json</c>.
/// </summary>
public static class IcpcStandingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static string SanitizeKey(string key)
        => new(key.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-').ToArray());

    private static string PathFor(IWebHostEnvironment env, string key)
        => Path.Combine(env.WebRootPath, "standings", $"{SanitizeKey(key)}.json");

    /// <summary>Whether stored standings already exist for a key.</summary>
    public static bool Exists(IWebHostEnvironment env, string key)
        => File.Exists(PathFor(env, key));

    public static async Task SaveAsync(IWebHostEnvironment env, string key, IcpcStandings standings, CancellationToken ct = default)
    {
        var dir = Path.Combine(env.WebRootPath, "standings");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(standings, JsonOptions);
        await File.WriteAllTextAsync(PathFor(env, key), json, ct);
    }

    /// <summary>Deserializes the stored standings for a key, or null if it doesn't exist.</summary>
    public static async Task<IcpcStandings?> ReadAsync(IWebHostEnvironment env, string key, CancellationToken ct = default)
    {
        var path = PathFor(env, key);
        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<IcpcStandings>(json, JsonOptions);
    }

    public static List<string> ListKeys(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "standings");
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
