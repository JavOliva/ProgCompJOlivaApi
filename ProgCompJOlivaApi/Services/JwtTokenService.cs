using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ProgCompJOlivaApi.Models;

namespace ProgCompJOlivaApi.Services;

public class JwtTokenService(IConfiguration configuration)
{
    public (string token, DateTime expiresAtUtc) CreateAccessToken(User user, TimeSpan lifetime)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"]!;
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;

        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Nickname),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Nickname),
            new("token_use", "access")
        };

        foreach (var role in user.Roles.Select(r => r.RoleName))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expires);
    }

    /// <summary>
    /// Creates a stateless refresh token: a signed JWT carrying the user id and a
    /// <c>token_use=refresh</c> marker. Its lifetime is the chosen session duration.
    /// </summary>
    public (string token, DateTime expiresAtUtc) CreateRefreshToken(User user, TimeSpan lifetime)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"]!;
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;

        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("token_use", "refresh")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>
    /// Validates a refresh token (signature, issuer/audience, lifetime, and the
    /// <c>token_use=refresh</c> marker) and returns its user id and expiry, or null if invalid.
    /// </summary>
    public (Guid userId, DateTime expiresAtUtc)? ValidateRefreshToken(string token)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var key = jwtSection["Key"]!;

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt)
                return null;

            if (principal.FindFirst("token_use")?.Value != "refresh")
                return null;

            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(sub, out var userId))
                return null;

            return (userId, jwt.ValidTo);
        }
        catch
        {
            return null;
        }
    }
}
