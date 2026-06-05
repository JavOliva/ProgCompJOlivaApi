using System.Reflection.Emit;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Me.Dtos;
using ProgCompJOlivaApi.Data;

namespace ProgCompJOlivaApi.Controllers.Me;

[ApiController]
[Route("api/me")]
public class MeController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("navigation-context")]
    public async Task<ActionResult<NavigationContextResponse>> GetNavigation(CancellationToken ct = default)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct().ToList();
        var isAdmin = roles.Contains("Admin");

        Guid? userId = TryGetUserId();

        var response = new NavigationContextResponse
        {
            IsAuthenticated = isAuthenticated,
            Roles = roles,
            Permissions = new NavigationPermissionsDto
            {
                Views = new NavigationViewPermissionsDto
                {
                    Notes = true,
                    Ranking = true,
                    Training = true,
                    Contests = true,
                    Social = true,
                    Admin = isAdmin
                },
                Actions = new NavigationActionPermissionsDto
                {
                    CreateUser = isAdmin,
                    CreateOrganization = isAdmin
                }
            },
            NavigationData = new NavigationDataDto
            {
                TrainingItems = await GetTrainingItems(userId, ct),
                SocialItems =
                [
                    new NavigationItemDto 
                    {
                        Label = "Equipos ICPC!",
                        To = "/social/icpc-teams"
                    }
                ]
            }
        };

        return Ok(response);
    }

    private Guid? TryGetUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("sub") 
            ?? User.FindFirstValue("userId");

        if (Guid.TryParse(rawUserId, out var userId))
            return userId;

        return null;
    }

    private async Task<List<NavigationItemDto>> GetTrainingItems(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
        {
            return await db.Trainings
                .AsNoTracking()
                .Where(t => t.IsPublic)
                .OrderBy(t => t.Name)
                .Select(t => new NavigationItemDto
                {
                    Label = t.Name,
                    To = $"/training/{t.Slug}"
                })
                .ToListAsync(ct);
        }

        return await db.Trainings
            .AsNoTracking()
            .Where(t => t.IsPublic || t.Users.Any(u => u.Id == userId.Value))
            .OrderBy(t => t.Name)
            .Select(t => new NavigationItemDto
            {
                Label = t.Name,
                To = $"/training/{t.Slug}"
            })
            .ToListAsync(ct);
    }
}
