using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

public class CodeforcesClient : IJudgeClient
{
    public string JudgeName => "Codeforces";

    private readonly string? _key;
    private readonly string? _secret;
    private readonly string _address = "https://codeforces.com/api/";
    private readonly HttpClient _httpClient = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Process-wide throttle: at most one Codeforces call at a time, with a minimum gap measured
    // from the end of the previous response. Shared across every CodeforcesClient instance so the
    // ratings worker, the gym importer and the solve sync can't collectively exceed CF's limit.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(5);
    private static DateTime _lastCallUtc = DateTime.MinValue;

    /// <param name="key">API key (required only for authorized methods like gym standings).</param>
    /// <param name="secret">API secret (required only for authorized methods).</param>
    public CodeforcesClient(string? key = null, string? secret = null)
    {
        _key = key;
        _secret = secret;
        _httpClient.BaseAddress = new Uri(_address);
    }

    public Task ConnectAsync() => Task.CompletedTask;

    public Task StartAsync() => Task.CompletedTask;

    public async Task<IReadOnlyList<CodeforcesUser>> GetUsersInfoAsync(IEnumerable<string> handles, bool checkHistoricHandles = true, CancellationToken ct = default)
    {
        var cleanedHandles = handles
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleanedHandles.Count == 0)
            return [];

        var queryParams = new Dictionary<string, string?>
        {
            ["handles"] = string.Join(';', cleanedHandles),
            ["checkHistoricHandles"] = checkHistoricHandles ? "true" : "false"
        };

        var url = QueryHelpers.AddQueryString("user.info", queryParams);

        var body = await RateLimitedGetStringAsync(url, ct);

        var apiResponse = JsonSerializer.Deserialize<CodeforcesApiResponse<List<CodeforcesUser>>>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize the Codeforces response.");
        if (apiResponse.Status is CodeforcesResponseStatus.FAILED)
            throw new InvalidOperationException($"Codeforces API error: {apiResponse.Comment ?? "Unknown error"}");

        return apiResponse.Result ?? [];
    }

    public async Task<Dictionary<string, int>> GetUsersRatings(IEnumerable<string> handles, CancellationToken ct = default)
    {
        Dictionary<string, int> ret = [];

        foreach (var handle in handles)
            ret.TryAdd(handle, 0);

        var codeforcesUsers = await GetUsersInfoAsync(handles, true, ct);
        foreach (var user in codeforcesUsers)
        {
            if (!ret.TryGetValue(user.Handle, out _))
                continue;

            ret[user.Handle] = user.Rating ?? 0;
        }

        return ret;
    }

    /// <summary>
    /// Fetches a contest's standings (works for gyms). When <paramref name="handles"/> is given,
    /// only those competitors' rows are returned. Requires a configured API key/secret because
    /// gym standings are an authorized method.
    /// </summary>
    public async Task<CodeforcesStandings> GetContestStandingsAsync(long contestId, IEnumerable<string>? handles, bool showUnofficial, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_secret))
            throw new InvalidOperationException("Codeforces API key/secret are not configured (Codeforces:Key / Codeforces:Secret).");

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("apiKey", _key!),
            new("contestId", contestId.ToString()),
            new("count", "10000"),
            new("from", "1"),
            new("showUnofficial", showUnofficial ? "true" : "false"),
            new("time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var handleList = handles?
            .Select(h => h.Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (handleList is { Count: > 0 })
            parameters.Add(new("handles", string.Join(";", handleList)));

        var url = BuildSignedQuery("contest.standings", parameters);

        var body = await RateLimitedGetStringAsync(url, ct);

        var apiResponse = JsonSerializer.Deserialize<CodeforcesApiResponse<CodeforcesStandings>>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize the Codeforces standings response.");
        if (apiResponse.Status is CodeforcesResponseStatus.FAILED)
            throw new InvalidOperationException($"Codeforces API error: {apiResponse.Comment ?? "Unknown error"}");

        return apiResponse.Result ?? new CodeforcesStandings();
    }

    /// <summary>
    /// Builds a signed Codeforces query string. apiSig = rand + sha512Hex(rand/method?sortedParams#secret),
    /// with params sorted lexicographically by key then value.
    /// </summary>
    private string BuildSignedQuery(string method, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var rand = GenerateRand();

        var sorted = parameters
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .ThenBy(p => p.Value, StringComparer.Ordinal)
            .ToList();

        var paramString = string.Join("&", sorted.Select(p => $"{p.Key}={p.Value}"));

        var toHash = $"{rand}/{method}?{paramString}#{_secret}";
        var hash = Convert.ToHexStringLower(SHA512.HashData(Encoding.UTF8.GetBytes(toHash)));

        return $"{method}?{paramString}&apiSig={rand}{hash}";
    }

    private static string GenerateRand()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var buffer = new char[6];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(buffer);
    }

    private async Task<string> RateLimitedGetStringAsync(string url, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var wait = MinInterval - (DateTime.UtcNow - _lastCallUtc);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);

            try
            {
                using var response = await _httpClient.GetAsync(url, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Codeforces returned {(int)response.StatusCode} ({response.StatusCode}). Body: {body}", null, response.StatusCode);

                return body;
            }
            finally
            {
                // Measure the gap from the end of this call, per the 5s-after-response rule.
                _lastCallUtc = DateTime.UtcNow;
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}
