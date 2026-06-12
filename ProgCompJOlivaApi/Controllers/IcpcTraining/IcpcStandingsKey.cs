using System.Text.RegularExpressions;

namespace ProgCompJOlivaApi.Controllers.IcpcTraining;

/// <summary>
/// Maps a standings key / seed file name to its (org, year, fase) coordinates and back.
/// Keys look like <c>uchile2024</c> (single phase) or <c>usm2024-1</c> / <c>usm2024-2</c> (phases).
/// </summary>
public static partial class IcpcStandingsKey
{
    [GeneratedRegex(@"^([a-zA-Z]+)(\d{4})(?:-(\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    /// <summary>
    /// Normalizes an organization name to the canonical key form: lower-cased, with the
    /// <c>utfsm</c> ⇄ <c>usm</c> aliases collapsed to <c>usm</c>.
    /// </summary>
    public static string NormalizeOrg(string org)
    {
        var o = (org ?? "").Trim().ToLowerInvariant();
        return o is "utfsm" ? "usm" : o;
    }

    /// <summary>
    /// Parses a key such as <c>usm2024-1</c> into its parts. Returns false for keys that don't
    /// follow the <c>{org}{year}[-{fase}]</c> shape.
    /// </summary>
    public static bool TryParse(string key, out string org, out int year, out int fase)
    {
        org = "";
        year = 0;
        fase = 1;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        var m = KeyRegex().Match(key.Trim());
        if (!m.Success)
            return false;

        org = NormalizeOrg(m.Groups[1].Value);
        year = int.Parse(m.Groups[2].Value);
        fase = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 1;
        return true;
    }
}
