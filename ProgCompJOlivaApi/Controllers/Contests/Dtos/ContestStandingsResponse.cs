namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

/// <summary>
/// Standings for a single contest. Only users who solved at least one of the contest's problems
/// appear in <see cref="Rows"/>; each row lists which of those problems the user solved.
/// </summary>
public class ContestStandingsResponse
{
    public Guid ContestId { get; set; }

    public string ContestName { get; set; } = null!;

    /// <summary>The contest's problems, ordered by position (the standings "columns").</summary>
    public List<ContestStandingProblemDto> Problems { get; set; } = [];

    /// <summary>One row per user with ≥1 solve, sorted by solved count desc then nickname.</summary>
    public List<ContestStandingRowDto> Rows { get; set; } = [];
}

public class ContestStandingProblemDto
{
    public Guid ProblemId { get; set; }

    public int Position { get; set; }

    public string Title { get; set; } = null!;

    public string Judge { get; set; } = null!;
}

public class ContestStandingRowDto
{
    public string Nickname { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? University { get; set; }

    public int SolvedCount { get; set; }

    /// <summary>Ids (referencing <see cref="ContestStandingsResponse.Problems"/>) this user solved.</summary>
    public List<Guid> SolvedProblemIds { get; set; } = [];
}
