using Microsoft.AspNetCore.Components.Web;

namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationViewPermissionsDto
{
    public bool Notes { get; set; }

    public bool Ranking { get; set; }

    public bool Training { get; set; }

    public bool Contests { get; set; }
    
    public bool Social { get; set; }

    public bool Admin { get; set; }
}
