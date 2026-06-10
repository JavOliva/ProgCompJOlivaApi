using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Authentication.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;

namespace ProgCompJOlivaApi.Controllers.Authentication;

[ApiController]
[Route("api/auth")]
public class AuthenticationController(AppDbContext db, PasswordService passwordService, JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Nickname and Password are required." });

        var user = await db.Users
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.Nickname == request.Nickname, ct);

        if (user is null || !passwordService.VerifyPassword(user, request.Password))
            return Unauthorized(new { error = "Invalid credentials." });

        if (!Enum.TryParse<SessionDurationDays>(request.SessionDuration, ignoreCase: true, out var sessionDuration))
            return BadRequest(new { error = "Invalid session duration." });

        var lifetime = sessionDuration switch
        {
            SessionDurationDays.One => TimeSpan.FromDays(1),
            SessionDurationDays.Thirty => TimeSpan.FromDays(30),
            SessionDurationDays.Forever => TimeSpan.FromDays(3650), // ~10 years; JWTs can't truly never expire
            _ => TimeSpan.FromDays(1)
        };

        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.CreateAccessToken(user, lifetime);

        await db.SaveChangesAsync(ct);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            Roles = [.. user.Roles.Select(r => r.RoleName)]
        });
    }
}
