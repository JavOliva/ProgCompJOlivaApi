using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;

namespace ProgCompJOlivaApi.Controllers.IcpcEvents;

/// <summary>
/// Parses a BOCA final-scoreboard page (e.g. scorelatam.naquadah.com.br) into
/// <see cref="IcpcEventStandings"/>.
///
/// Page shape: one <c>#myscoretable</c> where every team row is DUPLICATED once per view it
/// belongs to, tagged <c>class="sitegroupN"</c>; the menu maps N → view name via
/// <c>onclick="javascript:toggleGroup(N)"</c>. Views: <c>Global</c> (official teams),
/// <c>Global+CCL</c> (official + extras), <c>Female_Teams</c>, plus one view per region
/// (Brasil, Caribbean, …; top-level menu entries are wrapped in <c>&lt;b&gt;</c>) and their
/// country sub-views. Cells read <c>submissions/minutes</c> ("2/58"; a balloon img marks
/// solved; "3/-" = 3 attempts unsolved); the last cell is <c>solved (penalty)</c>.
/// </summary>
public static partial class BocaScoreboardParser
{
    [GeneratedRegex(@"toggleGroup\((\d+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex ToggleGroupRegex();

    [GeneratedRegex(@"^sitegroup(\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SiteGroupRegex();

    [GeneratedRegex(@"^(\d+)\s*\((\d+)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex TotalCellRegex();

    [GeneratedRegex(@"^(\d+)/(\d+|-)$", RegexOptions.CultureInvariant)]
    private static partial Regex ProblemCellRegex();

    /// <summary>Views that are categories rather than regions ("PdA" is the official view of the
    /// Programadores de América boards, playing the role "Global" plays on the regional ones).</summary>
    private static readonly HashSet<string> NonRegionViews = new(StringComparer.OrdinalIgnoreCase)
    {
        "Global", "Global+CCL", "Female_Teams", "CCL", "PdA"
    };

    /// <summary>
    /// Canonical region names for the region code embedded in BOCA team ids
    /// (<c>team{region}{country}{number}</c>, e.g. <c>teamsoch029</c> → <c>so</c> = South).
    /// Used by old editions (2019/2020) whose menus don't expose region views.
    /// </summary>
    private static readonly Dictionary<string, string> RegionNamesByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["br"] = "Brasil",
        ["cb"] = "Caribbean",
        ["ca"] = "CentroAmerica",
        ["mx"] = "Mexico",
        ["no"] = "North",
        ["so"] = "South",
    };

    [GeneratedRegex(@"^(?:team|ccl)([a-z]{2})[a-z]{2}\d+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TeamIdRegex();

    public static IcpcEventStandings Parse(string html, string @event, int year)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // --- Menu: group id -> (view name, top-level?) ---
        var groups = new Dictionary<int, (string Name, bool TopLevel)>();
        foreach (var anchor in doc.DocumentNode.SelectNodes("//a[contains(@onclick,'toggleGroup')]")
                 ?? Enumerable.Empty<HtmlNode>())
        {
            var match = ToggleGroupRegex().Match(anchor.GetAttributeValue("onclick", ""));
            if (!match.Success)
                continue;

            var id = int.Parse(match.Groups[1].Value);
            var name = HtmlEntity.DeEntitize(anchor.InnerText).Trim().Trim('(', ')').Trim();
            if (name.Length == 0 || groups.ContainsKey(id))
                continue;

            // Top-level entries have their NAME in bold. Comparing texts (instead of just checking
            // for a <b>) avoids false positives like "South<b>)</b>" (a bolded closing paren).
            var boldText = HtmlEntity.DeEntitize(string.Concat(
                    anchor.SelectNodes(".//b")?.Select(b => b.InnerText) ?? Enumerable.Empty<string>()))
                .Trim().Trim('(', ')').Trim();

            groups[id] = (name, TopLevel: boldText.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        if (groups.Count == 0)
            throw new FormatException("No toggleGroup menu found — is this a BOCA scoreboard page?");

        int? FindGroup(string name) => groups
            .Where(g => g.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(g => (int?)g.Key)
            .FirstOrDefault();

        // The official view is "Global" on regional boards; on PdA boards it's the first group
        // ("PdA" — BOCA opens group 1 by default, so group 1 is always the official view).
        var globalGroup = FindGroup("Global");
        var officialGroup = globalGroup ?? groups.Keys.Min();
        var globalCclGroup = FindGroup("Global+CCL");
        var femaleGroup = FindGroup("Female_Teams");

        // Regions: top-level menu entries that aren't the category views (some editions name
        // their CCL views "Brasil-CCL" etc. at top level — those aren't regions either).
        var regionGroups = groups
            .Where(g => g.Value.TopLevel
                        && !NonRegionViews.Contains(g.Value.Name)
                        && !g.Value.Name.Contains("ccl", StringComparison.OrdinalIgnoreCase))
            .OrderBy(g => g.Key)
            .ToList();

        // --- Table ---
        var table = doc.DocumentNode.SelectSingleNode("//table[@id='myscoretable']")
            ?? throw new FormatException("No #myscoretable found — is this a BOCA scoreboard page?");

        // Header row (no class): # | User/Site | Name | <labels…> | Total.
        var headerRow = table.SelectSingleNode(".//tr[not(@class)]")
            ?? throw new FormatException("No header row found in the scoreboard table.");
        var headerCells = headerRow.SelectNodes("./td") ?? throw new FormatException("Malformed header row.");
        if (headerCells.Count < 5)
            throw new FormatException("Unexpected header row shape.");

        var problemLabels = headerCells
            .Skip(3).Take(headerCells.Count - 4)
            .Select(td => CleanText(td.InnerText))
            .ToList();

        var standings = new IcpcEventStandings
        {
            Event = @event,
            Year = year,
            Contest = ExtractContestName(doc) ?? $"{@event} {year}",
            Regions = regionGroups.Select(g => g.Value.Name).ToList(),
            Problems = problemLabels.Select(l => new IcpcEventProblem { Label = l }).ToList()
        };

        // --- Rows: group the duplicated instances by team id ---
        var byTeam = new Dictionary<string, TeamAccumulator>(StringComparer.OrdinalIgnoreCase);
        var teamOrder = new List<string>(); // encounter order within the "all teams" view

        var orderingGroup = globalCclGroup ?? officialGroup; // best view containing every team

        foreach (var tr in table.SelectNodes(".//tr[@class]") ?? Enumerable.Empty<HtmlNode>())
        {
            var groupMatch = SiteGroupRegex().Match(tr.GetAttributeValue("class", "").Trim());
            if (!groupMatch.Success)
                continue;
            var groupId = int.Parse(groupMatch.Groups[1].Value);

            var cells = tr.SelectNodes("./td");
            if (cells is null || cells.Count != headerCells.Count)
                continue;

            var (teamId, country) = ParseUserSiteCell(cells[1]);
            if (teamId.Length == 0)
                continue;

            if (!byTeam.TryGetValue(teamId, out var team))
            {
                team = new TeamAccumulator
                {
                    Row = ParseRow(cells, problemLabels, teamId, country)
                };
                byTeam[teamId] = team;
            }

            team.Groups.Add(groupId);

            if (groupId == officialGroup && int.TryParse(CleanText(cells[0].InnerText), out var rank))
                team.Row.OfficialRank = rank;

            if (groupId == orderingGroup)
                teamOrder.Add(teamId);
        }

        if (byTeam.Count == 0)
            throw new FormatException("No team rows parsed from the scoreboard table.");

        // Teams outside the ordering view (defensive) go at the end, in first-seen order.
        foreach (var teamId in byTeam.Keys)
            if (!teamOrder.Contains(teamId))
                teamOrder.Add(teamId);

        foreach (var teamId in teamOrder)
        {
            var team = byTeam[teamId];
            var row = team.Row;

            row.Ccl = !team.Groups.Contains(officialGroup);
            row.Female = femaleGroup is int f && team.Groups.Contains(f);
            row.Region = regionGroups.FirstOrDefault(rg => team.Groups.Contains(rg.Key)).Value.Name ?? "";

            standings.Rows.Add(row);
        }

        // CCL teams only appear in per-country CCL views, so they miss the region groups. Fill
        // their region from the country → region mapping learned from the official teams.
        var regionByCountry = standings.Rows
            .Where(r => r.Region.Length > 0 && r.Country.Length > 0)
            .GroupBy(r => r.Country)
            .ToDictionary(g => g.Key, g => g.First().Region);

        foreach (var row in standings.Rows)
            if (row.Region.Length == 0 && regionByCountry.TryGetValue(row.Country, out var region))
                row.Region = region;

        // PdA-style boards (official view ≠ "Global"): the whole championship is one pool — the
        // official view is simply "everyone but CCL" and the menu sub-views are per-country
        // shortcuts, so regions don't apply. Clear them (the frontend hides the Región filter).
        if (globalGroup is null)
        {
            standings.Regions = [];
            foreach (var row in standings.Rows)
                row.Region = "";
            return standings;
        }

        // Old editions (2019/2020) expose no region views in the menu at all: fall back to the
        // region code embedded in the team id, applied uniformly so names stay consistent.
        if (standings.Regions.Count == 0)
        {
            foreach (var row in standings.Rows)
            {
                var idMatch = TeamIdRegex().Match(row.TeamId);
                if (row.Region.Length == 0 && idMatch.Success
                    && RegionNamesByCode.TryGetValue(idMatch.Groups[1].Value, out var regionName))
                {
                    row.Region = regionName;
                }
            }

            standings.Regions = standings.Rows
                .Select(r => r.Region)
                .Where(r => r.Length > 0)
                .Distinct()
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return standings;
    }

    private sealed class TeamAccumulator
    {
        public required IcpcEventStandingsRow Row { get; init; }
        public HashSet<int> Groups { get; } = [];
    }

    /// <summary>Parses the team-invariant parts of a row (identity, name cell, problem cells, total).</summary>
    private static IcpcEventStandingsRow ParseRow(
        HtmlNodeCollection cells, List<string> problemLabels, string teamId, string country)
    {
        var row = new IcpcEventStandingsRow
        {
            TeamId = teamId,
            Country = country
        };

        ParseNameCell(cells[2], row);

        for (var i = 0; i < problemLabels.Count; i++)
            row.Cells.Add(ParseProblemCell(cells[3 + i], problemLabels[i]));

        var totalMatch = TotalCellRegex().Match(CleanText(cells[^1].InnerText));
        if (totalMatch.Success)
        {
            row.Solved = int.Parse(totalMatch.Groups[1].Value);
            row.Penalty = int.Parse(totalMatch.Groups[2].Value);
        }

        return row;
    }

    /// <summary>
    /// Extracts <c>(teamId, country)</c> from the "User/Site" cell ("teamsoar027/AR"). The country
    /// comes from the flag image's alt (an ISO-ish code, stable across editions); the text after
    /// the slash is only a fallback — in old editions (2019/2020) it holds the REGION code instead.
    /// </summary>
    private static (string TeamId, string Country) ParseUserSiteCell(HtmlNode cell)
    {
        var text = CleanText(cell.InnerText);
        var slash = text.LastIndexOf('/');
        var teamId = (slash < 0 ? text : text[..slash]).Trim();

        var flagAlt = cell.SelectSingleNode(".//img")?.GetAttributeValue("alt", "").Trim() ?? "";
        var suffix = slash >= 0 ? text[(slash + 1)..].Trim() : "";

        // Only two-letter alpha codes are countries (extras like teamccl001 show "ccl" in both
        // the flag alt and the suffix — those teams simply have no country).
        var country = IsCountryCode(flagAlt) ? flagAlt.ToUpperInvariant()
            : IsCountryCode(suffix) ? suffix.ToUpperInvariant()
            : "";

        return (teamId, country);

        static bool IsCountryCode(string s) => s.Length == 2 && s.All(char.IsAsciiLetter);
    }

    /// <summary>
    /// Parses the name cell: "[UNICAMP] Team Name" optionally followed by a <c>&lt;br&gt;</c> and a
    /// qualification marker ("🎖️ <b>Qualified for …</b>").
    /// </summary>
    private static void ParseNameCell(HtmlNode cell, IcpcEventStandingsRow row)
    {
        var parts = cell.InnerHtml.Split(["<br>", "<br/>", "<br />"], StringSplitOptions.None);

        var first = CleanText(StripTags(parts[0]));
        if (first.StartsWith('[') && first.IndexOf(']') is var close and > 0)
        {
            var prefix = first[1..close].Trim();
            first = first[(close + 1)..].Trim();

            // "[CCL]" is a category marker, not an institution (the Ccl flag covers it).
            if (!prefix.Equals("CCL", StringComparison.OrdinalIgnoreCase))
                row.Institution = prefix;
        }
        row.Name = first;

        if (parts.Length > 1)
        {
            var note = CleanText(StripTags(string.Join(" ", parts.Skip(1))));
            // Drop the medal emoji / decorations; keep the sentence.
            note = string.Concat(note.Where(c => !char.IsSurrogate(c) && c != '️' && c != '\uD83C')).Trim();
            if (note.Length > 0)
                row.QualifiedNote = note;
        }
    }

    private static IcpcEventCell ParseProblemCell(HtmlNode cell, string label)
    {
        var result = new IcpcEventCell { Label = label };

        var match = ProblemCellRegex().Match(CleanText(cell.InnerText));
        if (!match.Success)
            return result; // untried ("&nbsp;&nbsp;")

        result.Attempted = true;
        var submissions = int.Parse(match.Groups[1].Value);

        if (match.Groups[2].Value == "-")
        {
            result.FailedAttempts = submissions;
        }
        else
        {
            result.Solved = true;
            result.SolveTimeMinutes = int.Parse(match.Groups[2].Value);
            result.FailedAttempts = Math.Max(0, submissions - 1);
        }

        return result;
    }

    /// <summary>The banner text ("ICPC Latin America Regionals 2025 - Site | SCORE | …") up to the separator.</summary>
    private static string? ExtractContestName(HtmlDocument doc)
    {
        var banner = doc.DocumentNode.SelectSingleNode("//td[@width='99%']");
        if (banner is null)
            return null;

        var text = CleanText(banner.InnerText);
        foreach (var separator in new[] { " - Site", "|" })
        {
            var idx = text.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                text = text[..idx];
        }

        text = text.Trim();
        return text.Length > 0 ? text : null;
    }

    private static string StripTags(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText;
    }

    private static string CleanText(string text)
        => Regex.Replace(HtmlEntity.DeEntitize(text) ?? "", @"\s+", " ").Trim();
}
