namespace ProgCompJOlivaApi.Models;

public class Contest
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<ContestProblem> ContestProblems { get; set; } = [];
}
