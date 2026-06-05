namespace ProgCompJOlivaApi.Controllers;

public static class Constants
{
    public const string AdminRole = "Admin";

    public const string UserRole = "User";

    public static List<string> AllowedRoles => [AdminRole, UserRole];
}

/// <summary>Canonical online-judge identifiers stored in <c>Problem.Judge</c>.</summary>
public static class Judges
{
    public const string Codeforces = "Codeforces";

    public const string AtCoder = "AtCoder";

    public const string Cses = "Cses";

    public const string LeetCode = "LeetCode";

    public const string CodeChef = "CodeChef";

    public const string Luogu = "Luogu";

    public static List<string> All => [Codeforces, AtCoder, Cses, LeetCode, CodeChef, Luogu];
}
