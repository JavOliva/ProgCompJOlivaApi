namespace ProgCompJOlivaApi.Models;

/// <summary>
/// A Codeforces gym registered as a source of problems. The list tells the system which gyms
/// to pull from; <see cref="FetchMethod"/> records how each one should be fetched. Importing
/// the problems is a separate concern not implemented here.
/// </summary>
public class CodeforcesGym
{
    public Guid Id { get; set; }

    /// <summary>The Codeforces gym contest id (the number in /gym/{id}). Unique.</summary>
    public int GymContestId { get; set; }

    /// <summary>Optional human-friendly name.</summary>
    public string? Name { get; set; }

    /// <summary>How this gym's problems should be fetched.</summary>
    public GymFetchMethod FetchMethod { get; set; } = GymFetchMethod.Standings;

    /// <summary>Whether this gym is active as a source.</summary>
    public bool Enabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
