namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>Compact problem shape used by the task-search list.</summary>
public class ProblemListItemDto
{
    public Guid Id { get; set; }

    public string Judge { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public int? Difficulty { get; set; }

    public List<string> Topics { get; set; } = [];

    public List<string> Keywords { get; set; } = [];

    public bool HasStatement { get; set; }

    public bool IsActive { get; set; }
}
