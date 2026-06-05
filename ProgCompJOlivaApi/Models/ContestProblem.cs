namespace ProgCompJOlivaApi.Models;

public class ContestProblem
{
    public Guid Id { get; set; }

    public Guid ContestId { get; set; }

    public Contest Contest { get; set; } = null!;

    public Guid ProblemId { get; set; }

    public Problem Problem { get; set; } = null!;

    public int Position { get; set; }
}
