namespace ProgCompJOlivaApi.Controllers.Users.Dtos;

public class ModifyUserRequest
{
    public string? Nickname { get; set; }

    public string? Password { get; set; }

    public string? Email { get; set; }

    public string? Names { get; set; }

    public string? Surnames { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? OrganizationShortName { get; set; }

    public bool? FemTeamEligible { get; set; }

    public bool? IsCompetitiveProgrammingActive { get; set; }

    public string? CodeforcesHandle { get; set; }

    public string? AtcoderHandle { get; set; }

    public string? CsesHandle { get; set; }

    public string? CsesId { get; set; }

    public string? CodeChefHandle { get; set; }

    public string? LuoguHandle { get; set; }

    public string? LeetCodeHandle { get; set; }

    public List<string>? Roles { get; set; }
}
