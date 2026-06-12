namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class TrainingListItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public bool IsActive { get; set; }

    public int ContestCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
