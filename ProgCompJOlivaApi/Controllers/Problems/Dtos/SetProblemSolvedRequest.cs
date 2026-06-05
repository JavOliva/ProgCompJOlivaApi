namespace ProgCompJOlivaApi.Controllers.Problems.Dtos;

/// <summary>
/// Sets whether a problem is solved. By default it applies to the caller; an Admin may
/// target another user via <see cref="UserNickname"/>.
/// </summary>
public class SetProblemSolvedRequest
{
    public bool IsSolved { get; set; }

    /// <summary>
    /// Optional. When set, the status is recorded for this user instead of the caller.
    /// Only Admins may set this for someone other than themselves.
    /// </summary>
    public string? UserNickname { get; set; }

    public string? Notes { get; set; }
}
