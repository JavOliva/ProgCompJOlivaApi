namespace ProgCompJOlivaApi.Controllers.Users.Dtos;

public class UserRankingItemDto
{
    public string Id { get; set; } = "1";

    public string Nickname { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? University { get; set; }

    public string? UniversityLogo { get; set; }

    public bool FemTeamEligible { get; set; }

    /// <summary>Active user — surfaced in the ranking as the "Participante ICPC" filter.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether the user is still active in competitive programming.</summary>
    public bool IsCompetitiveProgrammingActive { get; set; }

    public bool IcpcEligible { get; set; }

    public UserRatingsDto Ratings { get; set; } = new();
}
