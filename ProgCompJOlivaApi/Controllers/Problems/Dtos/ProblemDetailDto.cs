namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>Full problem shape returned by the detail endpoint.</summary>
public class ProblemDetailDto
{
    public Guid Id { get; set; }

    public string Judge { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public int? ContestId { get; set; }

    public string? ContestProblemId { get; set; }

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }

    public string? StatementPath { get; set; }

    public List<string> Topics { get; set; } = [];

    public List<string> Keywords { get; set; } = [];

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
