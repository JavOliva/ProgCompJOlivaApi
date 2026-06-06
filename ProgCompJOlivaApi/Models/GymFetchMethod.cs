namespace ProgCompJOlivaApi.Models;

/// <summary>
/// Strategy used to fetch a Codeforces gym's problems. Stored as a string in the database so
/// new strategies can be added without renumbering. For now only <see cref="Standings"/> is
/// implemented; future options might fetch differently (e.g. HTML scrape, a custom endpoint).
/// </summary>
public enum GymFetchMethod
{
    /// <summary>Read the gym's problem list from the Codeforces <c>contest.standings</c> API.</summary>
    Standings = 0
}
