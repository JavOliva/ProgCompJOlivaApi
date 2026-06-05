namespace ProgCompJOlivaApi.Models;

public class TrainingContest
{
    public Guid Id { get; set; }

    public Guid TrainingId { get; set; }

    public Training Training { get; set; } = null!;

    public Guid ContestId { get; set; }

    public Contest Contest { get; set; } = null!;

    public int Position { get; set; }
}