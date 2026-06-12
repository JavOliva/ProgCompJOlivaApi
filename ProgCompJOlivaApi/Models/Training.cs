namespace ProgCompJOlivaApi.Models;

public class Training
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<TrainingContest> TrainingContests { get; set; } = [];

    public List<User> Users { get; set; } = [];

    public List<TrainingParticipant> Participants { get; set; } = [];
}
