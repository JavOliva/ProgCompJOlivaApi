namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class UpdateTrainingRequest
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool? IsPublic { get; set; }

    public bool? IsActive { get; set; }
}
