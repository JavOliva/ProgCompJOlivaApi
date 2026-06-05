namespace ProgCompJOlivaApi.Models;

public class UserRole
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string RoleName { get; set; } = null!;
}
