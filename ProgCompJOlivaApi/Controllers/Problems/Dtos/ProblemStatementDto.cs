namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>A problem's stored statement, as an HTML fragment with MathJax math delimiters.</summary>
public class ProblemStatementDto
{
    public string Judge { get; set; } = null!;

    public string ExternalId { get; set; } = null!;

    public string Title { get; set; } = null!;

    /// <summary>HTML fragment with <c>\(...\)</c> / <c>\[...\]</c> math, ready for MathJax.</summary>
    public string Html { get; set; } = null!;
}
