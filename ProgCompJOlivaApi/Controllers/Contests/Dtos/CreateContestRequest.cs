namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

public class CreateContestRequest
{
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional problems to add at creation, in the given order. May be empty to create an
    /// empty contest and add problems later.
    /// </summary>
    public List<Guid> ProblemIds { get; set; } = [];
}