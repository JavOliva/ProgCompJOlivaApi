namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationContextResponse
{
    public bool IsAuthenticated { get; set; }

    public List<string> Roles { get; set; } = [];

    public NavigationPermissionsDto Permissions { get; set; } = new();

    public NavigationDataDto NavigationData { get; set; } = new();
}
