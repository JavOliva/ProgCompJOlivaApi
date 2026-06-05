namespace ProgCompJOlivaApi.Controllers.Users.Dtos;

public class CreateUserRequest
{
    public string Nickname { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Names { get; set; } = null!;

    public string Surnames { get; set; } = null!;

    public DateOnly DateOfBirth { get; set; }

    public string OrganizationShortName { get; set; } = null!;

    public bool FemTeamEligible { get; set; }

    public bool IsCompetitiveProgrammingActive { get; set; } = false;

    public string? CodeforcesHandle { get; set; }

    public string? AtcoderHandle { get; set; }

    public string? CsesHandle { get; set; }

    public string? CsesId { get; set; }

    public string? CodeChefHandle { get; set; }

    public string? LuoguHandle { get; set; }

    public string? LeetCodeHandle { get; set; }

    public List<string> Roles { get; set; } = [];
}