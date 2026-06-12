namespace ProgCompJOlivaApi.Controllers.CodeforcesGyms.Dtos;

public class CodeforcesGymDto
{
    public Guid Id { get; set; }

    public int GymContestId { get; set; }

    public string? Name { get; set; }

    /// <summary>Fetch strategy name, e.g. "Standings".</summary>
    public string FetchMethod { get; set; } = null!;

    public bool Enabled { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
