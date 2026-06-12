namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>
/// Creates a CSES problem. CSES problems come from the single global problemset, so the
/// only identifier is the problem's numeric id (e.g. <c>1068</c>).
/// </summary>
public class CreateCsesProblemRequest
{
    public required string Title { get; set; }

    public required string Url { get; set; }

    /// <summary>CSES problem id, e.g. <c>1068</c>.</summary>
    public required string CsesId { get; set; }

    public int? Difficulty { get; set; }

    public string? TagsJson { get; set; }

    public List<string> Keywords { get; set; } = [];

    public List<string> Topics { get; set; } = [];

    public string? StatementPath { get; set; }
}
