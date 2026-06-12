using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ProgCompJOlivaApi.JudgeClients.CsesClient;

/// <summary>
/// Scrapes which CSES problemset tasks a user has solved. CSES exposes this only to a
/// logged-in session, so requests carry a configured service-account cookie
/// (<c>Cses:SessionCookie</c> = the PHPSESSID value). A logged-in account can view any user's
/// statistics page, so one cookie serves all users.
///
/// On the page each task is an anchor whose class encodes the result:
/// <c>task-score icon full</c> = solved, <c>... zero</c> = attempted, <c>task-score icon</c> = untried.
/// </summary>
public partial class CsesSolvedScraper(IConfiguration configuration)
{
    private static readonly HttpClient Http = new();

    private readonly string? _sessionCookie = configuration["Cses:SessionCookie"];

    /// <summary>
    /// Returns the set of CSES task ids the given user has solved (the values match
    /// <c>Problem.ExternalId</c> for CSES problems). Throws if the service cookie is missing or
    /// no longer authenticated.
    /// </summary>
    public async Task<HashSet<string>> GetSolvedTaskIdsAsync(string csesUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csesUserId))
            return [];

        if (string.IsNullOrWhiteSpace(_sessionCookie))
            throw new InvalidOperationException("CSES session cookie is not configured (set Cses:SessionCookie).");

        var url = $"https://cses.fi/problemset/user/{csesUserId.Trim()}/";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"PHPSESSID={_sessionCookie}");
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (ProgCompJOliva CSES sync)");

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);

        // A logged-out / expired session renders the "please login" stub instead of statistics.
        // Fail loudly rather than silently reporting zero solves (which would wipe solve state).
        if (html.Contains("Please login", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CSES session is not authenticated (the configured cookie may have expired).");

        return ParseSolvedTaskIds(html);
    }

    /// <summary>Parses solved task ids out of a logged-in CSES statistics page.</summary>
    public static HashSet<string> ParseSolvedTaskIds(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var solved = new HashSet<string>();

        var anchors = document.DocumentNode
            .SelectNodes("//a[contains(@class,'task-score') and contains(@href,'/problemset/task/')]");

        if (anchors is null)
            return solved;

        foreach (var anchor in anchors)
        {
            // Class is a space-separated list; "full" means the task was accepted (100 pts).
            var classes = anchor.GetAttributeValue("class", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!classes.Contains("full"))
                continue;

            var match = TaskIdRegex().Match(anchor.GetAttributeValue("href", ""));
            if (match.Success)
                solved.Add(match.Groups[1].Value);
        }

        return solved;
    }

    [GeneratedRegex(@"/problemset/task/(\d+)")]
    private static partial Regex TaskIdRegex();
}
