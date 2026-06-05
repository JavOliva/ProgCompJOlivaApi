namespace ProgCompJOlivaApi.Controllers;

public static class Constants
{
    public const string AdminRole = "Admin";

    public const string UserRole = "User";

    public static List<string> AllowedRoles => [AdminRole, UserRole];
}
