namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

public class CreateCodeforcesProblemRequest
{
    public required string Title { get; set; }

    public required string Url { get; set; }

    public required int ContestId { get; set; }

    public required string ContestProblemId { get; set; }

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }

    /// <summary>Free-form search keywords.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Topic names this problem belongs to (created on demand).</summary>
    public List<string> Topics { get; set; } = [];

    /// <summary>Relative path to the locally-stored statement folder.</summary>
    public string? StatementPath { get; set; }
}