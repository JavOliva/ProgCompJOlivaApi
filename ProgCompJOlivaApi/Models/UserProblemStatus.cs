namespace ProgCompJOlivaApi.Models;

public class UserProblemStatus
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid ProblemId { get; set; }

    public Problem Problem { get; set; } = null!;

    public bool IsSolved { get; set; }

    public DateTime? SolvedAtUtc { get; set; }

    public DateTime? LastCheckedAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
