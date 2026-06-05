namespace ProgCompJOlivaApi.Models;

public class Problem
{
    public Guid Id { get; set; }

    public string Judge { get; set; } = null!;

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public string ExternalId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
