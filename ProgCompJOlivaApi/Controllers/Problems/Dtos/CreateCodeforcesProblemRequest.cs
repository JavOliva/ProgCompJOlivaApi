namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

public class CreateCodeforcesProblemRequest
{
    public required string Title { get; set; }

    public required string Url { get; set; }

    public required int ContestId { get; set; }

    public required string ContestProblemId { get; set; }

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }
}