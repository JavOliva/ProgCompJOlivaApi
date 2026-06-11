using Microsoft.AspNetCore.Hosting;

namespace ProgCompJOlivaApi.Utility;

/// <summary>
/// Stores and reads problem statements as HTML files under
/// <c>wwwroot/statements/{judge}/{externalId}/statement.html</c>. The returned relative path is
/// what gets saved in <c>Problem.StatementPath</c>.
/// </summary>
public static class StatementStore
{
    public static string RelativePath(string judge, string externalId)
        => $"/statements/{Sanitize(judge)}/{Sanitize(externalId)}/statement.html";

    public static async Task<string> SaveAsync(IWebHostEnvironment env, string judge, string externalId, string html, CancellationToken ct = default)
    {
        var relative = RelativePath(judge, externalId);
        var full = ToFullPath(env, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, html, ct);
        return relative;
    }

    public static async Task<string?> ReadAsync(IWebHostEnvironment env, string? statementPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(statementPath))
            return null;

        var full = ToFullPath(env, statementPath);
        if (!File.Exists(full))
            return null;

        return await File.ReadAllTextAsync(full, ct);
    }

    private static string ToFullPath(IWebHostEnvironment env, string relative)
        => Path.Combine(env.WebRootPath, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Makes a path-safe segment (CF external ids look like "567665/problem/A").</summary>
    private static string Sanitize(string value)
        => new(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
