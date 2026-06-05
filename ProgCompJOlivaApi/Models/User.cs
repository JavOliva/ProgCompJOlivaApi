namespace ProgCompJOlivaApi.Models;

public class User
{
    public Guid Id { get; set; }

    public string Nickname { get; set; } = null!;

    public string Email { get; set; } = null!;
    
    public string Names { get; set; } = null!;

    public string Surnames { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public bool FemTeamEligible { get; set; }

    public bool IsCompetitiveProgrammingActive { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    #region Codeforces
    public string? CodeforcesHandle { get; set; }

    public int CodeforcesRating { get; set; } = 0;
    #endregion

    #region Atcoder
    public string? AtcoderHandle { get; set; }

    public int AtcoderRating { get; set; } = 0;
    #endregion

    #region Cses
    public string? CsesHandle { get; set; }

    public string? CsesId { get; set; }

    public int CsesRating { get; set; } = 0;
    #endregion

    #region LeetCode
    public string? LeetCodeHandle { get; set; }

    public int LeetCodeRating { get; set; } = 0;
    #endregion

    #region CodeChef
    public string? CodeChefHandle { get; set; }

    public int CodeChefRating { get; set; } = 0;
    #endregion

    #region Luogu
    public string? LuoguHandle { get; set; }

    public int LuoguRating { get; set; } = 0;
    #endregion

    #region Authentication
    public string PasswordHash { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<UserRole> Roles { get; set; } = [];
    #endregion

    public List<Training> Trainings { get; set; } = [];
}
