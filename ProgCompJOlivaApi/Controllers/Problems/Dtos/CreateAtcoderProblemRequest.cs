namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>
/// Creates an AtCoder problem. AtCoder problems come from contests, e.g. contest
/// <c>abc300</c> task <c>abc300_a</c>.
/// </summary>
public class CreateAtcoderProblemRequest
{
    public required string Title { get; set; }

    public required string Url { get; set; }

    /// <summary>Contest screen name, e.g. <c>abc300</c>.</summary>
    public required string ContestId { get; set; }

    /// <summary>Task screen name (unique id on AtCoder), e.g. <c>abc300_a</c>.</summary>
    public required string TaskId { get; set; }

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }

    public List<string> Keywords { get; set; } = [];

    public List<string> Topics { get; set; } = [];

    public string? StatementPath { get; set; }
}
