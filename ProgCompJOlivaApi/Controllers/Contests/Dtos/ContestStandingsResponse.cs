namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

/// <summary>
/// Standings for a single contest: how many of the contest's problems each app user has
/// solved.
/// </summary>
public class ContestStandingsResponse
{
    public Guid ContestId { get; set; }

    public string ContestName { get; set; } = null!;

    public int ProblemCount { get; set; }

    public List<ContestStandingRowDto> Rows { get; set; } = [];
}

public class ContestStandingRowDto
{
    public string Nickname { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? University { get; set; }

    public int SolvedCount { get; set; }

    /// <summary>Ids of the contest problems this user has solved.</summary>
    public List<Guid> SolvedProblemIds { get; set; } = [];
}
