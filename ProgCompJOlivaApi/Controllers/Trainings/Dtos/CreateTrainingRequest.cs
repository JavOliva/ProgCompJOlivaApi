namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class CreateTrainingRequest
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional contests to add at creation, in the given order. May be empty to create an
    /// empty training and add contests later.
    /// </summary>
    public List<Guid> ContestIds { get; set; } = [];

    /// <summary>
    /// Optional participant nicknames (case-insensitive, matched against active users) to enroll
    /// at creation. Unknown/inactive nicknames cause a 400.
    /// </summary>
    public List<string> ParticipantNicknames { get; set; } = [];
}
