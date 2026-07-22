namespace ProgCompJOlivaApi.Controllers.IcpcEvents.Dtos;

/// <summary>
/// Standings of an international ICPC event (LATAM regional, Programadores de América, …)
/// imported from a BOCA scoreboard. One stored document per <c>{event}{year}</c> key. Rows keep
/// per-team metadata (country, region, CCL, female, members) so the frontend can filter without
/// re-importing.
/// </summary>
public class IcpcEventStandings
{
    /// <summary>Event slug: <c>latam</c>, <c>pda</c>, … (lowercase letters).</summary>
    public string Event { get; set; } = "";

    public int Year { get; set; }

    /// <summary>Display name, e.g. "ICPC Latin America Regionals 2025".</summary>
    public string Contest { get; set; } = "";

    /// <summary>The BOCA page this was imported from.</summary>
    public string SourceUrl { get; set; } = "";

    /// <summary>Top-level regions present in the scoreboard, in menu order (e.g. Brasil, South).</summary>
    public List<string> Regions { get; set; } = [];

    /// <summary>Problems in label order (the table columns).</summary>
    public List<IcpcEventProblem> Problems { get; set; } = [];

    /// <summary>One row per team, in the source's overall (official + extras) order.</summary>
    public List<IcpcEventStandingsRow> Rows { get; set; } = [];
}

public class IcpcEventProblem
{
    public string Label { get; set; } = "";
}

public class IcpcEventStandingsRow
{
    /// <summary>Rank in the official (Global) view; null for extra/unofficial teams (CCL).</summary>
    public int? OfficialRank { get; set; }

    /// <summary>BOCA team id (e.g. <c>teamsoar027</c>, <c>cclbrbr005</c>). Unique within the event.</summary>
    public string TeamId { get; set; } = "";

    /// <summary>Team name, without the institution prefix or qualification markers.</summary>
    public string Name { get; set; } = "";

    /// <summary>Institution short name (the <c>[UNICAMP]</c> prefix), or empty.</summary>
    public string Institution { get; set; } = "";

    /// <summary>
    /// Full name of the registered platform organization matching <see cref="Institution"/> (by
    /// <c>ShortName</c>, case-insensitive), or null. Set at read time; not stored.
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>The matched organization's logo URL (server-relative), or null.</summary>
    public string? OrganizationLogoUrl { get; set; }

    /// <summary>Two-letter country code as shown by the scoreboard (e.g. <c>CL</c>).</summary>
    public string Country { get; set; } = "";

    /// <summary>Top-level region the team belongs to (e.g. <c>South</c>), or empty.</summary>
    public string Region { get; set; } = "";

    /// <summary>Extra/unofficial team ("Café con Leche"): present only in the +CCL views.</summary>
    public bool Ccl { get; set; }

    /// <summary>Whether the team appears in the Female_Teams view.</summary>
    public bool Female { get; set; }

    /// <summary>Qualification marker from the scoreboard (e.g. "Qualified for Programadores de America in Chile"), or null.</summary>
    public string? QualifiedNote { get; set; }

    /// <summary>Team members (set by admins; the source doesn't list them). Enriched at read time.</summary>
    public List<IcpcEventTeamMember> Members { get; set; } = [];

    public int Solved { get; set; }

    public int Penalty { get; set; }

    /// <summary>Per-problem cells, aligned with <see cref="IcpcEventStandings.Problems"/>.</summary>
    public List<IcpcEventCell> Cells { get; set; } = [];
}

public class IcpcEventTeamMember
{
    /// <summary>The member's name as entered by an admin / the source (real name or handle).</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether <see cref="Name"/> matched a registered platform user (set at read time).</summary>
    public bool Registered { get; set; }

    /// <summary>The matched user's real name (Names + Surnames), or null.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The matched user's nickname, or null.</summary>
    public string? Nickname { get; set; }

    /// <summary>The matched user's Codeforces rating (for name coloring), or null.</summary>
    public int? Rating { get; set; }
}

public class IcpcEventCell
{
    public string Label { get; set; } = "";

    /// <summary>Whether the team submitted to this problem at all.</summary>
    public bool Attempted { get; set; }

    public bool Solved { get; set; }

    /// <summary>Failed submissions: before the AC if solved, else total.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Minutes into the contest when solved (if solved).</summary>
    public int? SolveTimeMinutes { get; set; }
}
