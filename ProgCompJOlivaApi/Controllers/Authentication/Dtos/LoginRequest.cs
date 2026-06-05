namespace ProgCompJOlivaApi.Controllers.Authentication.Dtos;

public class LoginRequest
{
    public string Nickname { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string SessionDuration { get; set; } = "One";
}
