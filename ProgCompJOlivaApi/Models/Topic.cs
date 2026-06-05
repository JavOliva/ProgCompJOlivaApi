namespace ProgCompJOlivaApi.Models;

/// <summary>
/// A controlled-vocabulary topic a <see cref="Problem"/> can belong to (e.g. "dp",
/// "graphs", "math"). Reused across problems via a many-to-many relationship so the
/// frontend can offer a consistent topic filter.
/// </summary>
public class Topic
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public List<Problem> Problems { get; set; } = [];
}
