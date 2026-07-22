using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;

namespace ProgCompJOlivaApi.Controllers.IcpcEvents;

/// <summary>
/// Parses an ICPC World Finals standings page from cphof.org (Competitive Programming Hall of
/// Fame) into <see cref="IcpcEventStandings"/>.
///
/// Page shape: the standings table's header is Rank | Country | Team | Score | Penalty | Prize.
/// Rank cell carries an optional medal img; Country cell has a flag anchor with the English name
/// in <c>title</c>; the Team cell has the university link, the team name in parentheses, and the
/// member profile links (so members come pre-filled); Prize lists champion titles / money.
/// There is no per-problem breakdown, so <see cref="IcpcEventStandings.Problems"/> stays empty.
/// </summary>
public static partial class CphofStandingsParser
{
    [GeneratedRegex(@"\((.+)\)\s*:?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TeamNameRegex();

    public static IcpcEventStandings Parse(string html, string @event, int year)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // The standings table is the one whose header starts with "Rank".
        var table = doc.DocumentNode.SelectNodes("//table")
            ?.FirstOrDefault(t => CleanText(t.SelectSingleNode(".//th")?.InnerText ?? "") == "Rank")
            ?? throw new FormatException("No standings table found — is this a cphof.org standings page?");

        var standings = new IcpcEventStandings
        {
            Event = @event,
            Year = year,
            Contest = $"ICPC World Finals {year}"
        };

        var index = 0;
        foreach (var tr in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count < 5)
                continue; // header or layout row

            index++;
            var row = new IcpcEventStandingsRow
            {
                // cphof rows have no team id; a stable synthetic one keeps member edits addressable.
                TeamId = $"wf{year}-{index:000}"
            };

            if (int.TryParse(CleanText(cells[0].InnerText), out var rank))
                row.OfficialRank = rank;

            // Country: the flag anchor's title holds the English name ("Chile").
            row.Country = cells[1].SelectSingleNode(".//a[@title]")?.GetAttributeValue("title", "")
                ?? CleanText(cells[1].InnerText);

            ParseTeamCell(cells[2], row);

            if (int.TryParse(CleanText(cells[3].InnerText), out var solved))
                row.Solved = solved;
            if (int.TryParse(CleanText(cells[4].InnerText), out var penalty))
                row.Penalty = penalty;

            // Prize cell: champion titles and money, one div each ("World Champion · $16200").
            if (cells.Count > 5)
            {
                var notes = cells[5].SelectNodes(".//div")
                    ?.Select(d => CleanText(d.InnerText))
                    .Where(t => t.Length > 0)
                    .ToList() ?? [];
                if (notes.Count > 0)
                    row.QualifiedNote = string.Join(" · ", notes);
            }

            standings.Rows.Add(row);
        }

        if (standings.Rows.Count == 0)
            throw new FormatException("No team rows parsed from the standings table.");

        return standings;
    }

    /// <summary>
    /// Team cell: first div = university link + "(Team Name):"; second div = member profile links.
    /// </summary>
    private static void ParseTeamCell(HtmlNode cell, IcpcEventStandingsRow row)
    {
        row.Institution = CleanText(cell.SelectSingleNode(".//a[contains(@href,'/university/')]")?.InnerText ?? "");

        var teamSpan = CleanText(cell.SelectSingleNode(".//span")?.InnerText ?? "");
        var match = TeamNameRegex().Match(teamSpan);
        row.Name = match.Success ? match.Groups[1].Value.Trim() : (row.Institution.Length > 0 ? row.Institution : teamSpan);
        if (row.Name.Length == 0)
            row.Name = row.Institution;

        row.Members = (cell.SelectNodes(".//a[contains(@href,'/profile/')]") ?? Enumerable.Empty<HtmlNode>())
            .Select(a => CleanText(a.InnerText))
            .Where(n => n.Length > 0)
            .Select(n => new IcpcEventTeamMember { Name = n })
            .ToList();
    }

    private static string CleanText(string text)
        => Regex.Replace(HtmlEntity.DeEntitize(text) ?? "", @"\s+", " ").Trim();
}
