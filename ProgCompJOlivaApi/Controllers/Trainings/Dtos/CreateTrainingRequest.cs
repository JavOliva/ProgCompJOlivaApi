namespace ProgCompJOlivaApi.Controllers.Trainings.Dtos;

public class CreateTrainingRequest
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public bool IsPublic { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
