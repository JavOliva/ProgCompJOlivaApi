namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class TrainingDetailDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Contests ordered by position.</summary>
    public List<TrainingContestDto> Contests { get; set; } = [];
}
