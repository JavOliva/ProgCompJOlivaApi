using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Users.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Services;

namespace ProgCompJOlivaApi.Controllers.Users;

[ApiController]
[Route("api/users")]
public class UserController(AppDbContext db, PasswordService passwordService) : ControllerBase
{
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nickname))
            return BadRequest(new { error = "Nickname is required." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (string.IsNullOrWhiteSpace(request.Names))
            return BadRequest(new { error = "Names are required." });

        if (string.IsNullOrWhiteSpace(request.Surnames))
            return BadRequest(new { error = "Surnames are required." });

        var nickname = request.Nickname.Trim();

        var nicknameExists = await db.Users
            .AnyAsync(x => x.Nickname == nickname, ct);

        if (nicknameExists)
            return Conflict(new { error = "Nickname already exists." });

        Organization? organization = null;

        if (!string.IsNullOrWhiteSpace(request.OrganizationShortName))
        {
            var organizationShortName = request.OrganizationShortName.Trim();

            organization = await db.Organizations
                .FirstOrDefaultAsync(x => x.ShortName == organizationShortName, ct);

            if (organization is null)
                return BadRequest(new { error = "Organization short name does not exist." });
        }

        if (organization == null)
            return BadRequest(new { error = "Organization does not exist." });

        var normalizedRoles = request.Roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedRoles.Count == 0)
            normalizedRoles.Add("User");

        foreach (var role in normalizedRoles)
        {
            if (!Constants.AllowedRoles.Contains(role))
                return BadRequest(new { error = $"Role {role} does not exist." });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Nickname = nickname,
            Names = request.Names.Trim() ?? string.Empty,
            Email = request.Email.Trim(),
            Surnames = request.Surnames.Trim() ?? string.Empty,
            DateOfBirth = request.DateOfBirth,
            OrganizationId = organization.Id,
            Organization = organization,
            FemTeamEligible = request.FemTeamEligible,
            IsCompetitiveProgrammingActive = request.IsCompetitiveProgrammingActive,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CodeforcesHandle = request.CodeforcesHandle,
            AtcoderHandle = request.AtcoderHandle,
            CsesHandle = request.CsesHandle,
            CsesId = request.CsesId,
            CodeChefHandle = request.CodeChefHandle,
            LuoguHandle = request.LuoguHandle,
            LeetCodeHandle = request.LeetCodeHandle
        };

        user.PasswordHash = passwordService.HashPassword(user, request.Password);

        db.Users.Add(user);

        foreach (var roleName in normalizedRoles)
        {
            db.UserRoles.Add(new UserRole
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                RoleName = roleName
            });
        }

        await db.SaveChangesAsync(ct);

        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{nickname}")]
    public async Task<IActionResult> ModifyUser(string nickname, [FromBody] ModifyUserRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Nickname == nickname && u.IsActive, ct);

        if (user == null)
            return NotFound(new { message = "Active user not found." });

        if (request.Nickname != null)
        {
            var nicknameExists = await db.Users
                .AnyAsync(x => x.Nickname == nickname, ct);

            if (nicknameExists)
                return Conflict(new { error = "Nickname already exists." });

            user.Nickname = request.Nickname;
        }

        // Admins can reset a user's password here. Ignore blank values so the field is optional.
        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = passwordService.HashPassword(user, request.Password);

        if (request.Email != null)
            user.Email = request.Email;

        if (request.Names != null) 
            user.Names = request.Names;

        if (request.Surnames != null) 
            user.Surnames = request.Surnames;

        if (request.FemTeamEligible.HasValue) 
            user.FemTeamEligible = request.FemTeamEligible.Value;

        if (request.IsCompetitiveProgrammingActive.HasValue) 
            user.IsCompetitiveProgrammingActive = request.IsCompetitiveProgrammingActive.Value;

        if (request.CodeforcesHandle != null) 
            user.CodeforcesHandle = request.CodeforcesHandle;

        if (request.AtcoderHandle != null) 
            user.AtcoderHandle = request.AtcoderHandle;

        if (request.CsesHandle != null) 
            user.CsesHandle = request.CsesHandle;

        if (request.CodeChefHandle != null) 
            user.CodeChefHandle = request.CodeChefHandle;

        if (request.LuoguHandle != null) 
            user.LuoguHandle = request.LuoguHandle;

        if (request.LeetCodeHandle != null) 
            user.LeetCodeHandle = request.LeetCodeHandle;

        if (request.OrganizationShortName != null)
        {
            var organizationShortName = request.OrganizationShortName.Trim();
            var organization = await db.Organizations
                .FirstOrDefaultAsync(o => o.ShortName == organizationShortName, ct);

            if (organization == null)
                return BadRequest(new { message = "New user organization not found." });

            user.OrganizationId = organization.Id;
            user.Organization = organization;
        }

        await db.SaveChangesAsync(ct);

        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{nickname}")]
    public async Task<IActionResult> DeleteUser(string nickname, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Nickname == nickname, ct);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (!user.IsActive)
            return BadRequest(new { message = "User is already deleted." });

        user.IsActive = false;
        user.DeletedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{nickname}")]
    public async Task<IActionResult> RestoreUser(string nickname, CancellationToken ct = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Nickname == nickname, ct);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (user.IsActive)
            return BadRequest(new { message = "User is already active." });

        user.IsActive = false;
        user.DeletedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("rankings")]
    public async Task<ActionResult<List<UserRankingItemDto>>> GetUsersRanking(CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(x => x.Organization)
            .Select(x => new UserRankingItemDto
            {
                FemTeamEligible = x.FemTeamEligible,
                IsActive = x.IsActive,
                IcpcEligible = true,
                Nickname = x.Nickname,
                FullName = $"{x.Names} {x.Surnames}".Trim(),
                University = x.Organization != null ? x.Organization.ShortName : null,
                UniversityLogo = x.Organization != null ? x.Organization.LogoUrl : null,
                Ratings = new UserRatingsDto
                {
                    Codeforces = x.CodeforcesRating,
                    Atcoder = x.AtcoderRating,
                    Cses = x.CsesRating,
                    Leetcode = x.LeetCodeRating,
                    Codechef = x.CodeChefRating,
                    Luogu = x.LuoguRating
                }
            })
            .OrderByDescending(x => x.Ratings.Codeforces)
            .ThenBy(x => x.Nickname)
            .ToListAsync(ct);

        return Ok(users);
    }
}