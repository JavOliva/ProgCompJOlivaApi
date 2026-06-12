namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

/// <summary>
/// Global training standings: for each app user, how many problems they solved in each
/// contest of the training plus the overall total. <see cref="Rows"/> cells line up with the
/// <see cref="Contests"/> order.
/// </summary>
public class TrainingStandingsResponse
{
    public Guid TrainingId { get; set; }

    public string TrainingName { get; set; } = null!;

    public List<TrainingStandingContestDto> Contests { get; set; } = [];

    public List<TrainingStandingRowDto> Rows { get; set; } = [];
}

public class TrainingStandingContestDto
{
    public Guid ContestId { get; set; }

    public string Name { get; set; } = null!;

    public int Position { get; set; }

    public int ProblemCount { get; set; }
}

public class TrainingStandingRowDto
{
    public string Nickname { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? University { get; set; }

    /// <summary>Solved counts per contest, in the same order as <c>Contests</c>.</summary>
    public List<TrainingStandingCellDto> PerContest { get; set; } = [];

    public int Total { get; set; }
}

public class TrainingStandingCellDto
{
    public Guid ContestId { get; set; }

    public int Solved { get; set; }
}
