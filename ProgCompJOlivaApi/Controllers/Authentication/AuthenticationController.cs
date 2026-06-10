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
    // Access tokens are short-lived and silently refreshed by the client; the refresh token's
    // lifetime (the chosen session duration) is what keeps the user signed in.
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);

    private static TimeSpan RefreshTokenLifetime(SessionDurationDays duration) => duration switch
    {
        SessionDurationDays.One => TimeSpan.FromDays(1),
        SessionDurationDays.Thirty => TimeSpan.FromDays(30),
        SessionDurationDays.Forever => TimeSpan.FromDays(3650), // ~10 years; JWTs can't truly never expire
        _ => TimeSpan.FromDays(1)
    };

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

        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.CreateAccessToken(user, AccessTokenLifetime);
        var (refreshToken, refreshTokenExpiresAtUtc) = jwtTokenService.CreateRefreshToken(user, RefreshTokenLifetime(sessionDuration));

        await db.SaveChangesAsync(ct);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            Roles = [.. user.Roles.Select(r => r.RoleName)]
        });
    }

    /// <summary>
    /// Exchanges a valid refresh token for a fresh access token. The refresh token is not
    /// rotated, so the session still ends when the original refresh token expires.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var validated = jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        if (validated is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        var (userId, refreshTokenExpiresAtUtc) = validated.Value;

        var user = await db.Users
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);

        if (user is null)
            return Unauthorized(new { error = "User not found or inactive." });

        var (accessToken, accessTokenExpiresAtUtc) = jwtTokenService.CreateAccessToken(user, AccessTokenLifetime);

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc,
            RefreshToken = request.RefreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            Roles = [.. user.Roles.Select(r => r.RoleName)]
        });
    }
}
