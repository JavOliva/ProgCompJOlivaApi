namespace ProgCompJOlivaApi.Controllers.CodeforcesGyms.Dtos;

/// <summary>Partial update of a gym. A <c>null</c> field is left unchanged.</summary>
public class UpdateCodeforcesGymRequest
{
    public int? GymContestId { get; set; }

    public string? Name { get; set; }

    public string? FetchMethod { get; set; }

    public bool? Enabled { get; set; }
}
