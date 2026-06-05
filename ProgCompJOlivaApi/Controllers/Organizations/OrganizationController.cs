using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgCompJOlivaApi.Controllers.Organizations.Dtos;
using ProgCompJOlivaApi.Data;
using ProgCompJOlivaApi.Models;
using ProgCompJOlivaApi.Utility;

namespace ProgCompJOlivaApi.Controllers.Organizations;

[ApiController]
[Route("api/organizations")]
public class OrganizationController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private readonly string[] _allowedLogoTypes = [".jpg", ".jpeg", ".png"];

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateOrganizationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required");

        if (string.IsNullOrWhiteSpace(request.ShortName))
            return BadRequest("ShortName is required");

        if (request.Logo == null)
            return BadRequest("Logo is required");

        var ext = Path.GetExtension(request.Logo.FileName).ToLower();

        if (!_allowedLogoTypes.Contains(ext))
            return BadRequest($"Invalid file type {ext}");

        if (request.Logo.Length > 2 * 1024 * 1024)
            return BadRequest("File too large");

        if (!request.Logo.ContentType.StartsWith("image/"))
            return BadRequest("File is not an image");

        var name = request.Name.Trim();
        var shortName = request.ShortName.Trim();

        var nameExists = await db.Organizations
            .AnyAsync(x => x.Name == name, ct);

        if (nameExists)
            return Conflict(new { error = "Organization name already exists." });

        var shortNameExists = await db.Organizations
            .AnyAsync(x => x.ShortName == shortName, ct);

        if (shortNameExists)
            return Conflict(new { error = "Organization short name already exists." });

        string logoUrl = "";
        try
        {
            logoUrl = await FileSystem.UploadOrganizationLogo(request.Logo, env);
            Console.WriteLine(logoUrl);
        }
        catch (Exception ex)
        {
            return BadRequest($"{ex.Message}");
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            ShortName = shortName,
            LogoUrl = logoUrl
        };

        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        return Ok();
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{shortName}")]
    public async Task<IActionResult> Modify(string shortName, [FromForm] ModifyOrganizationRequest request, CancellationToken ct = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(x => x.ShortName == shortName, ct);

        if (organization == null)
            return BadRequest($"Organization {shortName} not found");

        if (request.NewName == null && request.NewShortName == null && request.NewLogo == null)
            return BadRequest("No modifications requested");

        if (request.NewName != null && string.IsNullOrWhiteSpace(request.NewName))
            return BadRequest("New Name is empty but not null");

        if (request.NewShortName != null && string.IsNullOrWhiteSpace(request.NewShortName))
            return BadRequest("New ShortName is empty but not null");

        string? ext = null;
        if (request.NewLogo != null)
            ext = Path.GetExtension(request.NewLogo.FileName).ToLower();

        if (request.NewLogo != null && !_allowedLogoTypes.Contains(ext))
            return BadRequest($"Invalid file type {ext}");

        if (request.NewLogo != null && request.NewLogo.Length > 2 * 1024 * 1024)
            return BadRequest("File too large");

        if (request.NewLogo != null && !request.NewLogo.ContentType.StartsWith("image/"))
            return BadRequest("File is not an image");

        var newName = request.NewName?.Trim();
        var newShortName = request.NewShortName?.Trim();

        if (newShortName != null)
        {
            var shortNameExists = await db.Organizations
                .AnyAsync(x => x.ShortName == newShortName, ct);

            if (shortNameExists)
                return Conflict(new { error = "Organization short name already exists" });
        }

        if (newName != null)
        {
            var nameExists = await db.Organizations
                .AnyAsync(x => x.Name == newName, ct);

            if (nameExists)
                return Conflict(new { error = "Organization name already exists" });
        }

        string logoUrl = organization.LogoUrl!;
        if (request.NewLogo != null)
        {
            try
            {
                logoUrl = await FileSystem.ModifyOrganizationLogo(logoUrl, request.NewLogo, env);
            }
            catch (Exception ex)
            {
                return BadRequest($"{ex.Message}");
            }
        }

        if (newName != null)
            organization.Name = newName;

        if (newShortName != null)
            organization.ShortName = newShortName;

        organization.LogoUrl = logoUrl;

        await db.SaveChangesAsync(ct);

        return Ok();
    }
}
