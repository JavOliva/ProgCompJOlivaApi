namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class SidebarItemDto
{
    public string Label { get; set; } = "";

    public string? To { get; set; }

    public string? IconKey { get; set; }

    public List<SidebarItemDto> Children { get; set; } = [];
}
