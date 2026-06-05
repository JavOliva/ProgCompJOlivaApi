namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

/// <summary>A problem as it appears inside a contest, including its position.</summary>
public class ContestProblemDto
{
    public int Position { get; set; }

    public Guid ProblemId { get; set; }

    public string Judge { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public int? Difficulty { get; set; }
}
