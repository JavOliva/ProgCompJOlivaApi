namespace ProgCompJOlivaApi.Models;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = null!;

    public string ShortName { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public List<User> Users { get; set; } = [];
}
