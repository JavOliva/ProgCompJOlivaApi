namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationPermissionsDto
{
    public NavigationViewPermissionsDto Views { get; set; } = new();

    public NavigationActionPermissionsDto Actions { get; set; } = new();
}
