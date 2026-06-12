namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

/// <summary>
/// Replaces a training's full participant set with the users matching these nicknames
/// (case-insensitive, active users only).
/// </summary>
public class SetTrainingParticipantsRequest
{
    public List<string> Nicknames { get; set; } = [];
}
