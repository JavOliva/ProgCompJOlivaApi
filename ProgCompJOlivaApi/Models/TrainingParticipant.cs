namespace ProgCompJOlivaApi.Models;

/// <summary>
/// Join entity linking a <see cref="Training"/> to a participating <see cref="User"/>
/// (many-to-many). Distinct from the legacy implicit <c>Training.Users</c> relation.
/// </summary>
public class TrainingParticipant
{
    public Guid Id { get; set; }

    public Guid TrainingId { get; set; }

    public Training Training { get; set; } = null!;

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
