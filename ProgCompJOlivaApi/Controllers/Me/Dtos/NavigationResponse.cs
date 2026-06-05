namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationResponse
{
    public List<TopNavItemDto> TopNav { get; set; } = [];

    public List<SidebarSectionDto> Sections { get; set; } = [];
}
