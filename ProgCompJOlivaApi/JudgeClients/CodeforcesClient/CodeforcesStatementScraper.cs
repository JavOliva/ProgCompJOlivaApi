using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

/// <summary>
/// Extracts a Codeforces problem statement from its HTML page. Codeforces wraps the whole
/// statement (legend, input/output specs and sample tests) in <c>div.problem-statement</c> and
/// writes math as <c>$$$...$$$</c>; we make image URLs absolute and convert that math to MathJax
/// <c>\(...\)</c> delimiters. Gym problem pages require a logged-in session cookie (they 403
/// otherwise), passed in by the caller.
/// </summary>
public static partial class CodeforcesStatementScraper
{
    /// <summary>Returns the normalized statement fragment, or null if no statement div is present.</summary>
    public static string? Extract(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var node = document.DocumentNode
            .SelectSingleNode("//div[contains(concat(' ', normalize-space(@class), ' '), ' problem-statement ')]");
        if (node is null)
            return null;

        foreach (var img in node.SelectNodes(".//img[@src]") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", "");
            if (src.StartsWith("//"))
                img.SetAttributeValue("src", "https:" + src);
            else if (src.StartsWith('/'))
                img.SetAttributeValue("src", "https://codeforces.com" + src);
        }

        var fragment = node.OuterHtml;

        // Codeforces math ($$$...$$$) -> standard MathJax \(...\).
        fragment = CfMathRegex().Replace(fragment, m => $"\\({m.Groups[1].Value}\\)");

        return fragment;
    }

    [GeneratedRegex(@"\$\$\$(.+?)\$\$\$", RegexOptions.Singleline)]
    private static partial Regex CfMathRegex();
}
