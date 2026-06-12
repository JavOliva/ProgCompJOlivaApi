namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

public class ContestDetailDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Problems ordered by position.</summary>
    public List<ContestProblemDto> Problems { get; set; } = [];
}
