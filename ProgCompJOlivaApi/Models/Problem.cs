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

    /// <summary>
    /// Relative path (served from wwwroot) to this problem's statement folder. The folder
    /// can hold the statement, images, sample test cases and a <c>hints.md</c> file, so the
    /// problem is readable without the originating judge being online.
    /// </summary>
    public string? StatementPath { get; set; }

    /// <summary>Free-form search keywords. Stored as a PostgreSQL <c>text[]</c>.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>Topics this problem belongs to (many-to-many).</summary>
    public List<Topic> Topics { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
