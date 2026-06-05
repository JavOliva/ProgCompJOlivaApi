namespace ProgCompJOlivaApi.Controllers.Contests.Dtos;

public class ContestListItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public int ProblemCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
