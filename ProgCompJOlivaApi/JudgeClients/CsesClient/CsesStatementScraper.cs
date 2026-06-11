using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ProgCompJOlivaApi.JudgeClients.CsesClient;

/// <summary>
/// Fetches a CSES task's statement from its public page and returns it as an HTML fragment with
/// MathJax-standard math delimiters. CSES wraps math in <c>&lt;span class="math math-inline"&gt;LaTeX&lt;/span&gt;</c>
/// (and <c>math-display</c>); we convert those to <c>\(...\)</c> / <c>\[...\]</c> so any default
/// MathJax setup renders them. The statement <c>div.md</c> already contains the statement, the
/// Input/Output sections and the sample tests (<c>&lt;pre&gt;</c> blocks).
/// </summary>
public static partial class CsesStatementScraper
{
    private static readonly HttpClient Http = new();

    public static async Task<string?> GetStatementHtmlAsync(string taskId, CancellationToken ct = default)
    {
        var url = $"https://cses.fi/problemset/task/{taskId.Trim()}/";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (ProgCompJOliva statement import)");

        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync(ct);
        return Extract(html);
    }

    /// <summary>Extracts and normalizes the statement fragment from a CSES task page.</summary>
    public static string? Extract(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var md = document.DocumentNode
            .SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' md ')]");
        if (md is null)
            return null;

        // Make image/source URLs absolute so the frontend can load them.
        foreach (var img in md.SelectNodes(".//img[@src]") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", "");
            if (src.StartsWith('/'))
                img.SetAttributeValue("src", "https://cses.fi" + src);
        }

        var fragment = md.OuterHtml;

        // CSES math spans -> standard MathJax delimiters.
        fragment = MathDisplayRegex().Replace(fragment, m => $"\\[{m.Groups[1].Value}\\]");
        fragment = MathInlineRegex().Replace(fragment, m => $"\\({m.Groups[1].Value}\\)");

        return fragment;
    }

    [GeneratedRegex("<span class=\"math math-display\">(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex MathDisplayRegex();

    [GeneratedRegex("<span class=\"math math-inline\">(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex MathInlineRegex();
}
