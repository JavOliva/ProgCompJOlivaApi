using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers;
using ProgCompJOlivaApi.Controllers.Oci.Dtos;
using ProgCompJOlivaApi.Data;

namespace ProgCompJOlivaApi.Controllers.Oci;

/// <summary>
/// OCI (Olimpiada Chilena de InformÃ¡tica) standings: school-olympiad results with subtask scoring and
/// no penalty. Editions are grouped by type â€” <c>regional</c>, <c>nacional</c>, <c>clasificatoria</c> â€” and year.
/// Reading is public; uploading is Admin-only. Participants are matched to registered platform users
/// (real name + rating) at request time.
/// </summary>
[ApiController]
[Route("api/oci-standings")]
public class OciStandingsController(IWebHostEnvironment env, AppDbContext db) : ControllerBase
{
    /// <summary>Lists the raw keys of the available OCI standings (e.g. <c>regional2022</c>).</summary>
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<List<string>> List()
        => Ok(OciStandingsStore.ListKeys(env));

    /// <summary>
    /// Catalog of every available OCI standings, grouped by type (regional / nacional / ioi) â†’ year,
    /// so the frontend can discover which editions exist without hardcoding them. One entry per year
    /// (a multi-phase IOI year is a single entry with <c>phases &gt; 0</c>).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("catalog")]
    public async Task<ActionResult<List<OciStandingsCatalogType>>> Catalog(CancellationToken ct = default)
    {
        var all = await LoadAllAsync(ct);

        var catalog = all
            .GroupBy(x => x.Type)
            .OrderBy(g => Array.IndexOf(OciStandingsKey.Types, g.Key))
            .Select(g => new OciStandingsCatalogType
            {
                Type = g.Key,
                Editions = g.GroupBy(x => x.Year).OrderBy(y => y.Key)
                    .Select(y => ToEdition(g.Key, y.Key, y.ToList())).ToList()
            })
            .ToList();

        return Ok(catalog);
    }

    /// <summary>
    /// Lists the editions of a given type â€” use <c>regional</c>, <c>nacional</c> or <c>clasificatoria</c> to see
    /// which OCI Regionales / Nacionales / IOI exist (year + contest name + phase count), ordered by year.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{type}")]
    public async Task<ActionResult<List<OciStandingsCatalogEdition>>> ListByType(string type, CancellationToken ct = default)
    {
        var normalized = OciStandingsKey.NormalizeType(type);
        if (normalized is null)
            return NotFound(new { error = $"Unknown type '{type}'. Use one of: {string.Join(", ", OciStandingsKey.Types)}." });

        var editions = (await LoadAllAsync(ct))
            .Where(x => x.Type == normalized)
            .GroupBy(x => x.Year).OrderBy(y => y.Key)
            .Select(y => ToEdition(normalized, y.Key, y.ToList()))
            .ToList();

        return Ok(editions);
    }

    /// <summary>
    /// Returns the standings for a type and year as a single object (regional / nacional /
    /// single-phase IOI). For a multi-phase IOI clasificatoria it returns the weighted aggregate with
    /// the individual phases nested under <c>phases</c> (ordered by phase number). Participants are
    /// matched to registered users (real name + rating).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{type}/{year:int}")]
    public async Task<ActionResult<OciStandings>> Get(string type, int year, CancellationToken ct = default)
    {
        var normalized = OciStandingsKey.NormalizeType(type);
        if (normalized is null)
            return NotFound(new { error = $"Unknown type '{type}'. Use one of: {string.Join(", ", OciStandingsKey.Types)}." });

        var group = (await LoadAllAsync(ct))
            .Where(x => x.Type == normalized && x.Year == year)
            .ToList();
        if (group.Count == 0)
            return NotFound(new { error = $"No OCI {normalized} standings for {year}." });

        // The primary standings is the weighted aggregate if present (multi-phase IOI), else the
        // base-key one, else whatever single standings exists.
        var primary = group.FirstOrDefault(x => x.Standings.Weighted).Standings
                      ?? group.FirstOrDefault(x => x.Key == $"{normalized}{year}").Standings
                      ?? group[0].Standings;

        // Exclude `primary` itself (by reference) so it never appears under its own `Phases`. When
        // there's no weighted aggregate, `primary` is one of the phases; nesting it into its own
        // `Phases` would make a self-referential cycle that System.Text.Json rejects (â†’ 500).
        var phases = group
            .Where(x => x.Standings.Phase is not null && !x.Standings.Weighted
                        && !ReferenceEquals(x.Standings, primary))
            .OrderBy(x => x.Standings.Phase)
            .Select(x => x.Standings)
            .ToList();

        await EnrichWithUsersAsync(primary, ct);
        foreach (var ph in phases)
            await EnrichWithUsersAsync(ph, ct);
        primary.Phases = phases.Count > 0 ? phases : null;

        return Ok(primary);
    }

    /// <summary>
    /// Uploads OCI standings JSON under <paramref name="key"/> (e.g. <c>nacional2023</c>). Send the
    /// JSON as multipart form field <c>file</c> or as the raw request body. Type/year are taken from
    /// the key.
    /// </summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{key}")]
    public async Task<ActionResult<OciStandings>> Upload(string key, IFormFile? file, CancellationToken ct = default)
    {
        if (!OciStandingsKey.TryParse(key, out var type, out var year))
            return BadRequest(new { error = "Key must look like 'regional2022', 'nacional2023' or 'clasificatoria2024'." });

        string content;
        if (file is not null && file.Length > 0)
        {
            using var reader = new StreamReader(file.OpenReadStream());
            content = await reader.ReadToEndAsync(ct);
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            content = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "No standings JSON provided (multipart 'file' or raw body)." });

        OciStandings? standings;
        try
        {
            standings = System.Text.Json.JsonSerializer.Deserialize<OciStandings>(content,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse the standings JSON: {ex.Message}" });
        }

        if (standings is null || standings.Rows.Count == 0)
            return BadRequest(new { error = "Parsed 0 rows; is this a valid OCI standings JSON?" });

        standings.Type = type;
        standings.Year = year;

        await OciStandingsStore.SaveAsync(env, key, standings, ct);

        await EnrichWithUsersAsync(standings, ct);
        return Ok(standings);
    }

    /// <summary>
    /// Uploads an OCI standings for a year from a CSV. Columns are
    /// <c>Sede, Nombre, &lt;col1&gt;, â€¦, &lt;colN&gt;, TOTAL, Clasifica</c> â€” plus a trailing
    /// <c>Medalla</c> column (<c>ORO</c>/<c>PLATA</c>/<c>BRONCE</c>/<c>NA</c>) for nacional. The middle
    /// columns are read from the header (tasks for a regional/nacional/IOI-phase, or the phases for an
    /// IOI weighted result); <c>Clasifica</c> is 1/0. For an IOI clasificatoria pass <c>?phase=N</c> for
    /// a phase or <c>?weighted=true</c> for the final weighted result; omit both for a single-phase IOI.
    /// Regional/nacional don't take a phase. Send the CSV as multipart form field <c>file</c> or as the
    /// raw request body; pass an optional <c>contest</c> name. Replaces the matching standings if it
    /// already exists. The catalog and the standings endpoints reflect it immediately.
    /// </summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{type}/{year:int}/csv")]
    public async Task<ActionResult<OciStandings>> UploadCsv(
        string type, int year, IFormFile? file,
        [FromQuery] string? contest, [FromQuery] int? phase, [FromQuery] bool weighted,
        CancellationToken ct = default)
    {
        var normalized = OciStandingsKey.NormalizeType(type);
        if (normalized is null)
            return BadRequest(new { error = $"Unknown type '{type}'. Use one of: {string.Join(", ", OciStandingsKey.Types)}." });

        if (normalized != "clasificatoria" && (phase.HasValue || weighted))
            return BadRequest(new { error = "Only clasificatorias have phases / a weighted result." });
        if (phase is <= 0)
            return BadRequest(new { error = "phase must be a positive number." });
        if (phase.HasValue && weighted)
            return BadRequest(new { error = "A standings can't be both a phase and the weighted result." });

        string content;
        if (file is not null && file.Length > 0)
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            content = await reader.ReadToEndAsync(ct);
        }
        else
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            content = await reader.ReadToEndAsync(ct);
        }

        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "No CSV content provided (multipart 'file' or raw body)." });

        var contestName = string.IsNullOrWhiteSpace(contest)
            ? OciCsvParser.DefaultContestName(normalized, year, phase, weighted)
            : contest.Trim();

        OciStandings standings;
        try
        {
            standings = OciCsvParser.Parse(content, normalized, year, contestName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse the CSV: {ex.Message}" });
        }

        if (standings.Rows.Count == 0)
            return BadRequest(new { error = "The CSV has no data rows." });

        standings.Phase = phase;
        standings.Weighted = weighted;

        // A phase lives under {type}{year}-{phase}; the single / weighted standings under {type}{year}.
        var key = phase.HasValue ? $"{normalized}{year}-{phase}" : $"{normalized}{year}";
        await OciStandingsStore.SaveAsync(env, key, standings, ct);

        await EnrichWithUsersAsync(standings, ct);
        return Ok(standings);
    }

    /// <summary>Reads every stored standings with its key/type/year (used by the read endpoints).</summary>
    private async Task<List<(string Key, string Type, int Year, OciStandings Standings)>> LoadAllAsync(CancellationToken ct)
    {
        var result = new List<(string, string, int, OciStandings)>();
        foreach (var key in OciStandingsStore.ListKeys(env))
        {
            if (!OciStandingsKey.TryParse(key, out var type, out var year))
                continue;
            var standings = await OciStandingsStore.ReadAsync(env, key, ct);
            if (standings is not null)
                result.Add((key, type, year, standings));
        }
        return result;
    }

    /// <summary>
    /// Builds one catalog entry for a (type, year) group: the representative contest name (the weighted
    /// aggregate's if any, else the base key's) and the number of phases.
    /// </summary>
    private static OciStandingsCatalogEdition ToEdition(string type, int year,
        List<(string Key, string Type, int Year, OciStandings Standings)> items)
    {
        var main = items.FirstOrDefault(i => i.Standings.Weighted).Standings
                   ?? items.FirstOrDefault(i => i.Key == $"{type}{year}").Standings
                   ?? items[0].Standings;

        return new OciStandingsCatalogEdition
        {
            Year = year,
            Key = $"{type}{year}",
            Contest = main.Contest,
            Phases = items.Count(i => i.Standings.Phase is not null && !i.Standings.Weighted)
        };
    }

    /// <summary>
    /// Matches each row's username and real name against registered users (by Codeforces handle,
    /// nickname, or "Names Surnames", case-insensitive) and fills in the canonical name, nickname and
    /// Codeforces rating so the frontend can color the name by rating. Inactive users are matched
    /// too: standings are historical, and a deactivated user still played that edition.
    /// </summary>
    private async Task EnrichWithUsersAsync(OciStandings standings, CancellationToken ct)
    {
        var names = standings.Rows
            .SelectMany(r => new[] { r.Username, r.User })
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (names.Count == 0)
            return;

        var users = await db.Users
            .Where(u =>
                (u.CodeforcesHandle != null && names.Contains(u.CodeforcesHandle.ToLower()))
                 || names.Contains(u.Nickname.ToLower())
                 || names.Contains((u.Names + " " + u.Surnames).ToLower()))
            .Select(u => new { u.Nickname, u.CodeforcesHandle, u.Names, u.Surnames, u.CodeforcesRating })
            .ToListAsync(ct);

        var byName = new Dictionary<string, (string DisplayName, string Nickname, int Rating)>();
        foreach (var u in users)
        {
            var fullName = $"{u.Names} {u.Surnames}".Trim();
            var info = (fullName, u.Nickname, u.CodeforcesRating);
            byName[u.Nickname.ToLowerInvariant()] = info;
            byName[fullName.ToLowerInvariant()] = info;
            if (!string.IsNullOrEmpty(u.CodeforcesHandle))
                byName[u.CodeforcesHandle.ToLowerInvariant()] = info;
        }

        foreach (var row in standings.Rows)
        {
            var byUser = row.User?.Trim().ToLowerInvariant();
            var byHandle = row.Username?.Trim().ToLowerInvariant();
            if ((byHandle is not null && byName.TryGetValue(byHandle, out var info))
                || (byUser is not null && byName.TryGetValue(byUser, out info)))
            {
                row.Registered = true;
                row.DisplayName = info.DisplayName;
                row.Nickname = info.Nickname;
                row.Rating = info.Rating;
            }
        }
    }
}
