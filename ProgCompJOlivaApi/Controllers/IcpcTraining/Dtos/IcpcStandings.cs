namespace ProgCompJOlivaApi.Controllers.IcpcTraining.Dtos;

/// <summary>
/// ICPC-style standings computed from a contest <c>.dat</c> file. Teams are ranked by number of
/// problems solved (desc), then total penalty (asc).
/// </summary>
public class IcpcStandings
{
    public string Contest { get; set; } = "";

    /// <summary>Organization the contest belongs to, normalized (e.g. <c>uchile</c>, <c>usm</c>).</summary>
    public string Org { get; set; } = "";

    /// <summary>Year of the contest.</summary>
    public int Year { get; set; }

    /// <summary>Phase number within the year (1-based; 1 when the year has a single phase).</summary>
    public int Fase { get; set; } = 1;

    public int ContestLengthMinutes { get; set; }

    /// <summary>Penalty minutes added per wrong submission before an accepted one.</summary>
    public int PenaltyPerWrong { get; set; }

    /// <summary>Problems in label order (the table columns).</summary>
    public List<IcpcStandingsProblem> Problems { get; set; } = [];

    /// <summary>One row per team, already ranked.</summary>
    public List<IcpcStandingsRow> Rows { get; set; } = [];
}

public class IcpcStandingsProblem
{
    public string Label { get; set; } = "";

    public string Name { get; set; } = "";
}

public class IcpcStandingsRow
{
    public int Rank { get; set; }

    public int TeamId { get; set; }

    /// <summary>The team/handle name as it appears in the <c>.dat</c> file.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Whether <see cref="Name"/> was matched to a registered platform user. When true,
    /// <see cref="DisplayName"/>, <see cref="Nickname"/> and <see cref="Rating"/> are populated.
    /// </summary>
    public bool Registered { get; set; }

    /// <summary>The matched user's real name (Names + Surnames), or null if unmatched.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The matched user's platform nickname, or null if unmatched.</summary>
    public string? Nickname { get; set; }

    /// <summary>The matched user's Codeforces rating, so the frontend can color the name. Null if unmatched.</summary>
    public int? Rating { get; set; }

    public int Solved { get; set; }

    public int Penalty { get; set; }

    /// <summary>Per-problem cells, aligned with <see cref="IcpcStandings.Problems"/>.</summary>
    public List<IcpcStandingsCell> Problems { get; set; } = [];
}

public class IcpcStandingsCell
{
    public string Label { get; set; } = "";

    /// <summary>Whether the team submitted to this problem at all (any non-pending verdict).</summary>
    public bool Attempted { get; set; }

    public bool Solved { get; set; }

    /// <summary>Failed (penalty-incurring) submissions: before the AC if solved, else total.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Minutes into the contest when this problem was solved (if solved).</summary>
    public int? SolveTimeMinutes { get; set; }

    /// <summary>This problem's penalty contribution (0 when unsolved).</summary>
    public int Penalty { get; set; }
}
