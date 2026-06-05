namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class AddTrainingContestRequest
{
    public required Guid ContestId { get; set; }

    /// <summary>
    /// Optional 1-based position to insert at. When omitted (or out of range) the contest is
    /// appended at the end.
    /// </summary>
    public int? Position { get; set; }
}
