namespace ProgCompJOlivaApi.Controllers.Topics.Dtos;

public class TopicDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public int ProblemCount { get; set; }
}
