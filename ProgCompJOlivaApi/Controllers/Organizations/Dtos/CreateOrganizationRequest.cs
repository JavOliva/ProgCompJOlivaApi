namespace ProgCompJOlivaApi.Controllers.Organizations.Dtos;

public class CreateOrganizationRequest
{
    public string Name { get; set; } = null!;

    public string ShortName { get; set; } = null!;

    public required IFormFile Logo { get; set; }
}