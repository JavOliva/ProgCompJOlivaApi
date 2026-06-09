using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProgCompJOlivaApi.JudgeClients.CsesClient;

namespace ProgCompJOlivaApi.Controllers.Cses;

[ApiController]
[Route("api/cses")]
[Authorize(Roles = Constants.AdminRole)]
public class CsesController(CsesSolvedScraper scraper) : ControllerBase
{
    /// <summary>
    /// Returns the CSES task ids the given CSES user has solved, scraped from their statistics
    /// page. The ids match <c>Problem.ExternalId</c> for CSES problems.
    /// </summary>
    [HttpGet("user/{csesUserId}/solved")]
    public async Task<IActionResult> GetSolved(string csesUserId, CancellationToken ct = default)
    {
        try
        {
            var ids = await scraper.GetSolvedTaskIdsAsync(csesUserId, ct);

            return Ok(new
            {
                csesUserId,
                solvedCount = ids.Count,
                taskIds = ids.OrderBy(x => x, StringComparer.Ordinal).ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            // Missing/expired service cookie, or CSES returned the login stub.
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"Failed to reach CSES: {ex.Message}" });
        }
    }
}
