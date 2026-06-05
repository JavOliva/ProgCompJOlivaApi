namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>
/// Partial update of a problem's metadata. Every field is optional: a <c>null</c> value
/// means "leave unchanged". For <see cref="Keywords"/> and <see cref="Topics"/>, an empty
/// list clears them while <c>null</c> skips them.
/// </summary>
public class UpdateProblemRequest
{
    public string? Title { get; set; }

    public string? Url { get; set; }

    public int? Difficulty { get; set; }

    public bool? IsActive { get; set; }

    public string? TagsJson { get; set; }

    public string? StatementPath { get; set; }

    public List<string>? Keywords { get; set; }

    public List<string>? Topics { get; set; }
}
