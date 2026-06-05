namespace ProgCompJOlivaApi.Controllers.Organizations.Dtos;

public class ModifyOrganizationRequest
{
    public string? NewName { get; set; }

    public string? NewShortName { get; set; }

    public IFormFile? NewLogo { get; set; }
}
