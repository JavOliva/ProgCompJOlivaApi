using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ProgCompJOlivaApi.JudgeClients.CsesClient;

/// <summary>One task in the CSES problemset. <paramref name="Category"/> is the section
/// heading it appears under (e.g. "Introductory Problems").</summary>
public record CsesProblemInfo(string TaskId, string Title, string Url, string Category);

/// <summary>
/// Scrapes the full CSES problemset list. This page is public (unlike per-user statistics), so
/// no session cookie is needed.
/// </summary>
public static partial class CsesProblemsetScraper
{
    private const string ProblemsetUrl = "https://cses.fi/problemset/";

    private static readonly HttpClient Http = new();

    public static async Task<List<CsesProblemInfo>> GetAllProblemsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProblemsetUrl);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (ProgCompJOliva CSES import)");

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        return Parse(html);
    }

    /// <summary>Parses every <c>/problemset/task/{id}</c> link (id + title) from the list page.</summary>
    public static List<CsesProblemInfo> Parse(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var result = new List<CsesProblemInfo>();
        var seen = new HashSet<string>();

        var anchors = document.DocumentNode.SelectNodes("//a[contains(@href,'/problemset/task/')]");
        if (anchors is null)
            return result;

        foreach (var anchor in anchors)
        {
            var match = TaskIdRegex().Match(anchor.GetAttributeValue("href", ""));
            if (!match.Success)
                continue;

            var taskId = match.Groups[1].Value;
            if (!seen.Add(taskId))
                continue;

            var title = HtmlEntity.DeEntitize(anchor.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(title))
                continue;

            // The section heading (h2) the task list sits under, e.g. "Sorting and Searching".
            var heading = anchor.SelectSingleNode("preceding::h2[1]");
            var category = heading is null ? "Otros" : HtmlEntity.DeEntitize(heading.InnerText).Trim();
            if (string.IsNullOrWhiteSpace(category))
                category = "Otros";

            result.Add(new CsesProblemInfo(taskId, title, $"https://cses.fi/problemset/task/{taskId}/", category));
        }

        return result;
    }

    [GeneratedRegex(@"/problemset/task/(\d+)")]
    private static partial Regex TaskIdRegex();
}
