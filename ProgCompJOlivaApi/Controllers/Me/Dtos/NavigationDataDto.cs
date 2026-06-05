namespace ProgCompJOlivaApi.Controllers.Me.Dtos;

public class NavigationDataDto
{
    public List<NavigationItemDto> TrainingItems { get; set; } = [];

    public List<NavigtationItemDto> SocialItems { get; set; } = [];
}
