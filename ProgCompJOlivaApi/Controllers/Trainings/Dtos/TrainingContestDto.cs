namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

/// <summary>A contest as it appears inside a training, including its position.</summary>
public class TrainingContestDto
{
    public int Position { get; set; }

    public Guid ContestId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public int ProblemCount { get; set; }
}
