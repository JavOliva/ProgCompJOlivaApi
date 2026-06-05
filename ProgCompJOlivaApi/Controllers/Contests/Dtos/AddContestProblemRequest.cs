namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

public class AddContestProblemRequest
{
    public required Guid ProblemId { get; set; }

    /// <summary>
    /// Optional 1-based position to insert at. When omitted (or out of range) the problem is
    /// appended at the end.
    /// </summary>
    public int? Position { get; set; }
}
