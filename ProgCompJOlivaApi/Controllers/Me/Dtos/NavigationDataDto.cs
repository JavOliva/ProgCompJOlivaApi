namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationDataDto
{
    public List<NavigationItemDto> TrainingItems { get; set; } = [];

    public List<NavigationItemDto> SocialItems { get; set; } = [];
}
