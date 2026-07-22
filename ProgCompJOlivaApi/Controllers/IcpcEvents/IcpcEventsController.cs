using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;
using ProgCompJOlivaApi.Data;

namespace ProgCompJOlivaApi.Controllers.IcpcEvents;

/// <summary>
/// International ICPC event standings (LATAM regional, Programadores de América, …) imported
/// from BOCA scoreboard pages. Reading is public (frontend Ranking → ICPC views); importing and
/// editing team members is Admin-only. Member names are matched to registered platform users at
/// read time so the frontend can color them by rating.
/// </summary>
[ApiController]
[Route("api/icpc-events")]
public partial class IcpcEventsController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private static readonly HttpClient Http = new();

    [GeneratedRegex(@"^([a-z]+?)(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex(@"^[a-z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EventRegex();

    /// <summary>The available events and their years, derived from the stored standings.</summary>
    [AllowAnonymous]
    [HttpGet("catalog")]
    public ActionResult<List<object>> GetCatalog()
    {
        var entries = IcpcEventStandingsStore.ListKeys(env)
            .Select(key => KeyRegex().Match(key))
            .Where(m => m.Success)
            .GroupBy(m => m.Groups[1].Value)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                @event = g.Key,
                years = g.Select(m => int.Parse(m.Groups[2].Value)).OrderByDescending(y => y).ToList()
            })
            .ToList<object>();

        return Ok(entries);
    }

    /// <summary>One event edition's standings, with team members enriched against registered users.</summary>
    [AllowAnonymous]
    [HttpGet("{event}/{year:int}")]
    public async Task<ActionResult<IcpcEventStandings>> Get(string @event, int year, CancellationToken ct = default)
    {
        var standings = await IcpcEventStandingsStore.ReadAsync(env, IcpcEventStandingsStore.KeyFor(@event, year), ct);
        if (standings is null)
            return NotFound(new { error = "Standings not found." });

        await EnrichMembersAsync(standings, ct);
        await EnrichOrganizationsAsync(standings, ct);
        return Ok(standings);
    }

    /// <summary>
    /// Matches each row's scoreboard institution against the registered organizations — by
    /// <c>ShortName</c> ("UdeC" ↔ "UDEC") or full <c>Name</c> ("Universidad de Chile", as cphof
    /// writes it), case-insensitive — and fills in the full name + logo so the frontend can show
    /// the organization column.
    /// </summary>
    private async Task EnrichOrganizationsAsync(IcpcEventStandings standings, CancellationToken ct)
    {
        if (standings.Rows.All(r => r.Institution.Trim().Length == 0))
            return;

        // Few organizations exist; load them all and match accent-insensitively in memory
        // (cphof writes "Pontificia Universidad Católica de Chile" with the accent).
        var organizations = await db.Organizations
            .Select(o => new { o.ShortName, o.Name, o.LogoUrl })
            .ToListAsync(ct);

        var byName = new Dictionary<string, (string Name, string? LogoUrl)>();
        foreach (var org in organizations)
        {
            byName[NormalizeName(org.ShortName)] = (org.Name, org.LogoUrl);
            byName[NormalizeName(org.Name)] = (org.Name, org.LogoUrl);
        }

        foreach (var row in standings.Rows)
        {
            if (row.Institution.Trim().Length > 0
                && byName.TryGetValue(NormalizeName(row.Institution), out var org))
            {
                row.OrganizationName = org.Name;
                row.OrganizationLogoUrl = org.LogoUrl;
            }
        }
    }

    /// <summary>Lower-cases and strips accents so "Católica" matches "Catolica".</summary>
    private static string NormalizeName(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// Imports (or re-imports) an event edition from a BOCA scoreboard URL. Replaces the stored
    /// standings but preserves any team members admins already assigned (matched by team id).
    /// The download + parse can take a few seconds.
    /// </summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{event}/{year:int}/import")]
    public async Task<IActionResult> Import(
        string @event, int year, [FromQuery] string url, [FromQuery] string? contest,
        CancellationToken ct = default)
    {
        @event = (@event ?? "").Trim().ToLowerInvariant();
        if (!EventRegex().IsMatch(@event))
            return BadRequest(new { error = "Event must be lowercase letters only (e.g. 'latam', 'pda')." });

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { error = "url must be an absolute http(s) URL." });

        string html;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (ProgCompJOliva ICPC import)");
            using var response = await Http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Failed to fetch the scoreboard: {ex.Message}" });
        }

        IcpcEventStandings standings;
        try
        {
            // Parser by source: cphof.org hosts the World Finals standings; everything else
            // (scorelatam & friends) is a BOCA scoreboard.
            standings = uri.Host.Contains("cphof.org", StringComparison.OrdinalIgnoreCase)
                ? CphofStandingsParser.Parse(html, @event, year)
                : BocaScoreboardParser.Parse(html, @event, year);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { error = $"Could not parse the scoreboard: {ex.Message}" });
        }

        standings.SourceUrl = uri.ToString();
        if (!string.IsNullOrWhiteSpace(contest))
            standings.Contest = contest.Trim();

        // Re-importing must not lose admin-entered members.
        var key = IcpcEventStandingsStore.KeyFor(@event, year);
        var previous = await IcpcEventStandingsStore.ReadAsync(env, key, ct);
        if (previous is not null)
        {
            var membersByTeam = previous.Rows
                .Where(r => r.Members.Count > 0)
                .ToDictionary(r => r.TeamId, r => r.Members, StringComparer.OrdinalIgnoreCase);

            foreach (var row in standings.Rows)
                if (membersByTeam.TryGetValue(row.TeamId, out var members))
                    row.Members = members;
        }

        await IcpcEventStandingsStore.SaveAsync(env, key, standings, ct);

        return Ok(new
        {
            key,
            contest = standings.Contest,
            teams = standings.Rows.Count,
            officialTeams = standings.Rows.Count(r => !r.Ccl),
            cclTeams = standings.Rows.Count(r => r.Ccl),
            femaleTeams = standings.Rows.Count(r => r.Female),
            problems = standings.Problems.Count,
            regions = standings.Regions,
            countries = standings.Rows.Select(r => r.Country).Distinct().Order().ToList()
        });
    }

    /// <summary>Replaces a team's member list (names only; matching happens at read time).</summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPut("{event}/{year:int}/teams/{teamId}/members")]
    public async Task<IActionResult> SetMembers(
        string @event, int year, string teamId, [FromBody] SetTeamMembersRequest request,
        CancellationToken ct = default)
    {
        var key = IcpcEventStandingsStore.KeyFor(@event, year);
        var standings = await IcpcEventStandingsStore.ReadAsync(env, key, ct);
        if (standings is null)
            return NotFound(new { error = "Standings not found." });

        var row = standings.Rows.FirstOrDefault(r => r.TeamId.Equals(teamId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (row is null)
            return NotFound(new { error = "Team not found in these standings." });

        row.Members = (request.Names ?? [])
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n => new IcpcEventTeamMember { Name = n })
            .ToList();

        await IcpcEventStandingsStore.SaveAsync(env, key, standings, ct);

        return Ok(new { teamId = row.TeamId, members = row.Members.Select(m => m.Name).ToList() });
    }

    public class SetTeamMembersRequest
    {
        public List<string> Names { get; set; } = [];
    }

    /// <summary>
    /// Sets the country of every row matching one of the given handles (by team id or member name,
    /// case-insensitive). Meant to tag competitors a source didn't country-flag — e.g. marking the
    /// Chilean participants of the Maratona Feminina that CF didn't have a flag for. Pass a 2-letter
    /// code (<c>CL</c>) and the handles; region is derived for the known Latin-American codes.
    /// </summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPut("{event}/{year:int}/country")]
    public async Task<IActionResult> SetCountry(
        string @event, int year, [FromBody] SetCountryRequest request, CancellationToken ct = default)
    {
        var country = (request.Country ?? "").Trim().ToUpperInvariant();
        if (country.Length is not 2 || !country.All(char.IsAsciiLetterUpper))
            return BadRequest(new { error = "country must be a 2-letter code (e.g. CL)." });

        var handles = (request.Handles ?? [])
            .Select(h => h.Trim())
            .Where(h => h.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (handles.Count == 0)
            return BadRequest(new { error = "Provide at least one handle." });

        var key = IcpcEventStandingsStore.KeyFor(@event, year);
        var standings = await IcpcEventStandingsStore.ReadAsync(env, key, ct);
        if (standings is null)
            return NotFound(new { error = "Standings not found." });

        var region = RegionForCountry(country);
        var updated = new List<string>();
        foreach (var row in standings.Rows)
        {
            if (handles.Contains(row.TeamId) || row.Members.Any(m => handles.Contains(m.Name)))
            {
                row.Country = country;
                row.Region = region;
                updated.Add(row.TeamId);
            }
        }

        if (updated.Count == 0)
            return NotFound(new { error = "No rows matched the given handles.", handles = handles.ToList() });

        await IcpcEventStandingsStore.SaveAsync(env, key, standings, ct);
        return Ok(new { country, updated });
    }

    public class SetCountryRequest
    {
        public string Country { get; set; } = "";
        public List<string> Handles { get; set; } = [];
    }

    /// <summary>Region for a Latin-American country code (scorelatam scheme), or "" when unknown.</summary>
    private static string RegionForCountry(string code) => code switch
    {
        "BR" => "Brasil",
        "CL" or "AR" or "BO" or "PE" or "PY" or "UY" => "South",
        "MX" => "Mexico",
        "CR" or "SV" or "PA" or "NI" or "GT" or "HN" => "CentroAmerica",
        "CU" or "DO" or "PR" or "TT" or "JM" => "Caribbean",
        "CO" or "VE" or "EC" => "North",
        _ => "",
    };

    /// <summary>
    /// Matches member names against registered users (by Codeforces handle, nickname, or real
    /// "Names Surnames", case-insensitive; inactive users too — event rosters are historical) and
    /// fills in nickname + Codeforces rating so the frontend can color the member names. Also, when
    /// the source gave the row no institution (e.g. the MFP, where a row is just a CF handle), the
    /// row's organization is taken from the matched user's registered organization (so filtering by
    /// Chile shows the university logo).
    /// </summary>
    private async Task EnrichMembersAsync(IcpcEventStandings standings, CancellationToken ct)
    {
        var names = standings.Rows
            .SelectMany(r => r.Members)
            .Select(m => m.Name.Trim().ToLowerInvariant())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

        if (names.Count == 0)
            return;

        var users = await db.Users
            .Where(u =>
                (u.CodeforcesHandle != null && names.Contains(u.CodeforcesHandle.ToLower()))
                 || names.Contains(u.Nickname.ToLower())
                 || names.Contains((u.Names + " " + u.Surnames).ToLower()))
            .Select(u => new
            {
                u.Nickname,
                u.CodeforcesHandle,
                u.Names,
                u.Surnames,
                u.CodeforcesRating,
                OrgName = u.Organization != null ? u.Organization.Name : null,
                OrgLogo = u.Organization != null ? u.Organization.LogoUrl : null,
            })
            .ToListAsync(ct);

        var byName = new Dictionary<string, (string DisplayName, string Nickname, int Rating, string? OrgName, string? OrgLogo)>();
        foreach (var u in users)
        {
            var info = ($"{u.Names} {u.Surnames}".Trim(), u.Nickname, u.CodeforcesRating, u.OrgName, u.OrgLogo);
            byName[u.Nickname.ToLowerInvariant()] = info;
            byName[info.Item1.ToLowerInvariant()] = info;
            if (!string.IsNullOrEmpty(u.CodeforcesHandle))
                byName[u.CodeforcesHandle.ToLowerInvariant()] = info;
        }

        foreach (var row in standings.Rows)
        {
            foreach (var member in row.Members)
            {
                if (!byName.TryGetValue(member.Name.Trim().ToLowerInvariant(), out var info))
                    continue;

                member.Registered = true;
                member.DisplayName = info.DisplayName;
                member.Nickname = info.Nickname;
                member.Rating = info.Rating;

                // Fill the row's organization from a registered member when the source gave none.
                if (string.IsNullOrEmpty(row.Institution) && row.OrganizationName is null
                    && !string.IsNullOrEmpty(info.OrgName))
                {
                    row.OrganizationName = info.OrgName;
                    row.OrganizationLogoUrl = info.OrgLogo;
                }
            }
        }
    }
}
