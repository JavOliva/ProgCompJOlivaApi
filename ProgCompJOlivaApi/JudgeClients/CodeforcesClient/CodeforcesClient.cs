using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

public class CodeforcesClient : IJudgeClient
{
    public string JudgeName => "Codeforces";

    private readonly string _key = "8cab16f83329789da65de35c483ff6af31e9da22";

    private readonly string _secret = "d122f20946c4a84a877c654a4f5014b292b48e6e";

    private readonly string _address = "https://codeforces.com/api/";

    private readonly HttpClient _httpClient = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CodeforcesClient()
    {
        _httpClient.BaseAddress = new Uri(_address);
    }

    public async Task ConnectAsync()
    {
        return;
    }

    public async Task StartAsync()
    {
        return;
    }

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

        using var response = await _httpClient.GetAsync(url, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Codeforces respondió {(int)response.StatusCode} ({response.StatusCode}). Body: {body}", null, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CodeforcesApiResponse<List<CodeforcesUser>>>(body, _jsonOptions) ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de Codeforces.");
        if (apiResponse.Status is CodeforcesResponseStatus.FAILED)
            throw new InvalidOperationException($"Codeforces API error: {apiResponse.Comment ?? "Unknown error"}");

        return apiResponse.Result ?? [];
    }

    public async Task<Dictionary<string, int>> GetUsersRatings(IEnumerable<string> handles, CancellationToken ct = default)
    {
        Dictionary<string, int> ret = [];

        foreach (var handle in handles)
        {
            ret.TryAdd(handle, 0);
        }

        var codeforcesUsers = await GetUsersInfoAsync(handles, true, ct);
        foreach (var user in codeforcesUsers)
        {
            if (!ret.TryGetValue(user.Handle, out _))
                continue;

            ret[user.Handle] = user.Rating ?? 0;
        }

        return ret;
    }
}
