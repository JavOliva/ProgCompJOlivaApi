using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.IcpcTraining.Dtos;
using ProgCompJOlivaApi.Data;

namespace ProgCompJOlivaApi.Controllers.IcpcTraining;

/// <summary>
/// ICPC standings built from contest <c>.dat</c> files (seeded at startup or uploaded). Reading is
/// public (for the frontend table); uploading a <c>.dat</c> is Admin-only. Team names are matched to
/// registered platform users so the table can show real names and rating-based colors.
/// </summary>
[ApiController]
[Route("api/icpc-standings")]
public class IcpcStandingsController(IWebHostEnvironment env, AppDbContext db) : ControllerBase
{
    /// <summary>Lists the raw keys of the available standings.</summary>
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<List<string>> List()
        => Ok(IcpcStandingsStore.ListKeys(env));

    /// <summary>
    /// Catalog of every available standings, grouped org → year → phase (with the contest name and
    /// the key to fetch it). Lets the frontend discover which "selectivos" exist instead of
    /// hardcoding them. Build menus from this, then call <c>GET /api/icpc-standings/{org}/{year}</c>
    /// (or <c>/{key}</c>) to load a specific one.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("catalog")]
    public async Task<ActionResult<List<IcpcStandingsCatalogOrg>>> Catalog(CancellationToken ct = default)
    {
        var entries = new List<(string Org, int Year, int Fase, string Key, string Contest)>();
        foreach (var key in IcpcStandingsStore.ListKeys(env))
        {
            if (!IcpcStandingsKey.TryParse(key, out var org, out var year, out var fase))
                continue;
            var standings = await IcpcStandingsStore.ReadAsync(env, key, ct);
            entries.Add((org, year, fase, key, standings?.Contest ?? ""));
        }

        var catalog = entries
            .GroupBy(e => e.Org)
            .OrderBy(g => g.Key)
            .Select(g => new IcpcStandingsCatalogOrg
            {
                Org = g.Key,
                Years = g.GroupBy(e => e.Year)
                    .OrderBy(y => y.Key)
                    .Select(y => new IcpcStandingsCatalogYear
                    {
                        Year = y.Key,
                        Fases = y.OrderBy(e => e.Fase)
                            .Select(e => new IcpcStandingsCatalogFase { Fase = e.Fase, Key = e.Key, Contest = e.Contest })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return Ok(catalog);
    }

    /// <summary>
    /// Returns every standings for an organization and year, one element per phase ("fase"), ordered
    /// by phase number. A year with a single phase yields a list of length 1; a year with two phases
    /// yields length 2. <paramref name="org"/> accepts the usual aliases (e.g. <c>utfsm</c> == <c>usm</c>).
    /// Team names are matched to registered users (real name + rating) at request time.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{org}/{year:int}")]
    public async Task<ActionResult<List<IcpcStandings>>> GetByOrgYear(string org, int year, CancellationToken ct = default)
    {
        var normalizedOrg = IcpcStandingsKey.NormalizeOrg(org);

        var matching = IcpcStandingsStore.ListKeys(env)
            .Select(key => (key, parsed: IcpcStandingsKey.TryParse(key, out var o, out var y, out var f), o, y, f))
            .Where(x => x.parsed && x.o == normalizedOrg && x.y == year)
            .OrderBy(x => x.f)
            .ToList();

        var result = new List<IcpcStandings>();
        foreach (var (key, _, _, _, _) in matching)
        {
            var standings = await IcpcStandingsStore.ReadAsync(env, key, ct);
            if (standings is null)
                continue;
            await EnrichWithUsersAsync(standings, ct);
            result.Add(standings);
        }

        return Ok(result);
    }

    /// <summary>Returns a single stored standings by key, with team names matched to users.</summary>
    [AllowAnonymous]
    [HttpGet("{key}")]
    public async Task<ActionResult<IcpcStandings>> Get(string key, CancellationToken ct = default)
    {
        var standings = await IcpcStandingsStore.ReadAsync(env, key, ct);
        if (standings is null)
            return NotFound(new { error = "Standings not found." });

        await EnrichWithUsersAsync(standings, ct);
        return Ok(standings);
    }

    /// <summary>
    /// Uploads a contest <c>.dat</c> file under <paramref name="key"/>, computes the ICPC standings,
    /// stores them, and returns the result. Send the file as multipart form field <c>file</c>, or
    /// the raw <c>.dat</c> text as the request body. If <paramref name="key"/> follows the
    /// <c>{org}{year}[-{fase}]</c> shape (e.g. <c>usm2024-1</c>), org/year/fase are derived from it.
    /// </summary>
    [Authorize(Roles = Constants.AdminRole)]
    [HttpPost("{key}")]
    public async Task<ActionResult<IcpcStandings>> Upload(string key, IFormFile? file, CancellationToken ct = default)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            var source = file is not null && file.Length > 0 ? file.OpenReadStream() : Request.Body;
            await source.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        if (bytes.Length == 0)
            return BadRequest(new { error = "No .dat content provided (multipart 'file' or raw body)." });

        IcpcStandings standings;
        try
        {
            standings = DatStandingsParser.Parse(bytes);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Could not parse the .dat file: {ex.Message}" });
        }

        if (standings.Rows.Count == 0)
            return BadRequest(new { error = "Parsed 0 teams; is this a valid .dat file?" });

        if (IcpcStandingsKey.TryParse(key, out var org, out var year, out var fase))
        {
            standings.Org = org;
            standings.Year = year;
            standings.Fase = fase;
        }

        await IcpcStandingsStore.SaveAsync(env, key, standings, ct);

        await EnrichWithUsersAsync(standings, ct);
        return Ok(standings);
    }

    /// <summary>
    /// Matches each row's team name against active users (by Codeforces handle, nickname, or real
    /// "Names Surnames", case-insensitive) and fills in the real name, nickname and Codeforces rating
    /// so the frontend can display the real name and color it by rating. The real-name match matters
    /// for sources like Kattis that show full names instead of handles.
    /// </summary>
    private async Task EnrichWithUsersAsync(IcpcStandings standings, CancellationToken ct)
    {
        var names = standings.Rows
            .Select(r => r.Name?.Trim().ToLowerInvariant())
            .Where(n => !string.IsNullOrEmpty(n))
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

        // Map the handle, the nickname and the real name (all lowercased) to the user's display info.
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
            var lookup = row.Name?.Trim().ToLowerInvariant();
            if (lookup is not null && byName.TryGetValue(lookup, out var info))
            {
                row.Registered = true;
                row.DisplayName = info.DisplayName;
                row.Nickname = info.Nickname;
                row.Rating = info.Rating;
            }
        }
    }
}
