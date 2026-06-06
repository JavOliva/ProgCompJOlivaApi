namespace ProgCompJOlivaApi.Controllers.CodeforcesGyms.Dtos;

public class CreateCodeforcesGymRequest
{
    /// <summary>The Codeforces gym contest id (the number in /gym/{id}).</summary>
    public required int GymContestId { get; set; }

    public string? Name { get; set; }

    /// <summary>Fetch strategy name (e.g. "Standings"). Defaults to "Standings" when omitted.</summary>
    public string? FetchMethod { get; set; }

    public bool Enabled { get; set; } = true;
}
