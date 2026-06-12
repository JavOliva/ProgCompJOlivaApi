namespace ProgCompJOlivaApi.Controllers.Authentication.Dtos;

public class LoginResponse
{
    public string AccessToken { get; set; } = null!;

    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public string RefreshToken { get; set; } = null!;

    public DateTime RefreshTokenExpiresAtUtc { get; set; }

    public List<string> Roles { get; set; } = [];
}
