namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class SidebarSectionDto
{
    public string Key { get; set; } = "";

    public string SidebarTitle { get; set; } = "";

    public string MatchPrefix { get; set; } = "";

    public List<SidebarItemDto> Items { get; set; } = [];
}
