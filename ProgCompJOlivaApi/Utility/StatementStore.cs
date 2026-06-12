using Microsoft.AspNetCore.Hosting;

namespace ProgCompJOlivaApi.Utility;

/// <summary>
/// Reserves a per-problem folder for its statement under
/// <c>wwwroot/statements/{judge}/{externalId}/</c>. For now the folder is just created (empty);
/// statement content (HTML, images, PDF, …) will live here later. The relative path is stored in
/// <c>Problem.StatementPath</c>.
/// </summary>
public static class StatementStore
{
    public static string FolderRelativePath(string judge, string externalId)
        => $"/statements/{Sanitize(judge)}/{Sanitize(externalId)}";

    /// <summary>Creates the (empty) statement folder for a problem and returns its relative path.</summary>
    public static string EnsureFolder(IWebHostEnvironment env, string judge, string externalId)
    {
        var relative = FolderRelativePath(judge, externalId);
        var full = Path.Combine(env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(full);
        return relative;
    }

    /// <summary>Makes a path-safe segment (CF external ids look like "567665/problem/A").</summary>
    private static string Sanitize(string value)
        => new(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
