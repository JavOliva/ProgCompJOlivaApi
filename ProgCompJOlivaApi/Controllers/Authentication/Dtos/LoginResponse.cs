namespace ProgCompJOlivaApi.Controllers.Authentication.Dtos;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];
}
