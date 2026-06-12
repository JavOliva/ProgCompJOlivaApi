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
/// OCI (Olimpiada Chilena de Informática) standings: school-olympiad results with subtask scoring and
/// no penalty. Editions are grouped by type — <c>regional</c>, <c>nacional</c>, <c>clasificatoria</c> — and year.
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
    /// Catalog of every available OCI standings, grouped by type (regional / nacional / ioi) → year,
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
    /// Lists the editions of a given type — use <c>regional</c>, <c>nacional</c> or <c>clasificatoria</c> to see
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
        // `Phases` would make a self-referential cycle that System.Text.Json rejects (→ 500).
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
    /// <c>Sede, Nombre, &lt;col1&gt;, …, &lt;colN&gt;, TOTAL, Clasifica</c> — plus a trailing
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
            ? DefaultContestName(normalized, year, phase, weighted)
            : contest.Trim();

        OciStandings standings;
        try
        {
            standings = ParseOciCsv(content, normalized, year, contestName);
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

    /// <summary>Human-readable default contest name when the admin doesn't provide one.</summary>
    private static string DefaultContestName(string type, int year, int? phase, bool weighted) => type switch
    {
        "clasificatoria" when phase.HasValue => $"OCI Clasificatoria IOI {year} - Fase {phase}",
        "clasificatoria" when weighted => $"OCI Clasificatoria IOI {year} - Ponderado",
        "clasificatoria" => $"OCI Clasificatoria IOI {year}",
        "nacional" => $"Nacional {year}",
        _ => $"Regional {year}",
    };

    /// <summary>
    /// Parses an OCI CSV into standings. Columns are identified by their header <b>name</b>, so they
    /// can come in any order and the optional ones can be absent: <c>Nombre</c> (required) and
    /// <c>TOTAL</c>/<c>Global</c> (required); <c>Sede</c>/<c>Región</c>, <c>Clasifica</c> and
    /// <c>Medalla</c> are optional; a <c>#</c>/rank column is ignored; every other column is a task.
    /// A phase CSV typically has neither Sede, Clasifica nor Medalla. Ranks are recomputed by total desc.
    /// </summary>
    private static OciStandings ParseOciCsv(string content, string type, int year, string contestName)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2)
            throw new FormatException("Expected a header row and at least one data row.");

        var header = ParseCsvLine(lines[0]);

        int sedeIdx = -1, nameIdx = -1, totalIdx = -1, clasIdx = -1, medalIdx = -1;
        var taskIdx = new List<int>();
        for (var i = 0; i < header.Count; i++)
        {
            var h = Norm(header[i]);
            if (h is "sede" or "region" or "team" or "colegio" or "establecimiento") sedeIdx = i;
            else if (h is "nombre" or "name" or "participante" or "user") nameIdx = i;
            else if (h is "total" or "global") totalIdx = i;
            else if (h.StartsWith("clasif") || h == "qualified") clasIdx = i;
            else if (h is "medalla" or "medallas" or "medal") medalIdx = i;
            else if (h is "#" or "rank" or "puesto" or "lugar" or "n") { /* rank column — ignored */ }
            else taskIdx.Add(i);
        }

        if (nameIdx < 0)
            throw new FormatException("Missing a 'Nombre' column.");
        if (totalIdx < 0)
            throw new FormatException("Missing a 'TOTAL' (or 'Global') column.");
        if (taskIdx.Count == 0)
            throw new FormatException("No task columns found (every column except Nombre/Sede/TOTAL/Clasifica/Medalla/# is a task).");

        var standings = new OciStandings
        {
            Contest = contestName,
            Type = type,
            Year = year,
            Problems = taskIdx.Select(i => new OciStandingsProblem { Name = header[i].Trim(), MaxScore = 100 }).ToList()
        };

        static string Cell(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx].Trim() : "";

        var rows = new List<OciStandingsRow>();
        foreach (var line in lines.Skip(1))
        {
            var f = ParseCsvLine(line);
            var name = Cell(f, nameIdx);
            if (name.Length == 0)
                continue;

            rows.Add(new OciStandingsRow
            {
                Sede = Cell(f, sedeIdx),
                Username = "",
                User = name,
                Scores = taskIdx.Select(i => ParseScore(Cell(f, i))).ToList(),
                Global = ParseScore(Cell(f, totalIdx)),
                Qualified = clasIdx >= 0 && IsTruthy(Cell(f, clasIdx)),
                Medal = medalIdx >= 0 ? ParseMedal(Cell(f, medalIdx)) : null
            });
        }

        rows = rows.OrderByDescending(r => r.Global).ToList();
        for (var i = 0; i < rows.Count; i++)
            rows[i].Rank = i + 1;
        standings.Rows = rows;
        return standings;
    }

    /// <summary>Lower-cases and strips accents from a header cell, for accent-insensitive matching.</summary>
    private static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Normalizes a medal cell (<c>ORO</c>/<c>PLATA</c>/<c>BRONCE</c>) to lowercase, or null (NA/empty).</summary>
    private static string? ParseMedal(string s)
        => s.Trim().ToLowerInvariant() switch
        {
            "oro" or "gold" => "oro",
            "plata" or "silver" => "plata",
            "bronce" or "bronze" => "bronce",
            _ => null,
        };

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static int ParseScore(string s)
    {
        s = s.Trim();
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);
        return 0;
    }

    private static bool IsTruthy(string s)
        => s.Trim().ToLowerInvariant() is "1" or "true" or "si" or "sí" or "x" or "yes" or "y" or "verdadero";

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
    /// Matches each row's username and real name against active users (by Codeforces handle, nickname,
    /// or "Names Surnames", case-insensitive) and fills in the canonical name, nickname and Codeforces
    /// rating so the frontend can color the name by rating.
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
            .Where(u => u.IsActive &&
                ((u.CodeforcesHandle != null && names.Contains(u.CodeforcesHandle.ToLower()))
                 || names.Contains(u.Nickname.ToLower())
                 || names.Contains((u.Names + " " + u.Surnames).ToLower())))
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
