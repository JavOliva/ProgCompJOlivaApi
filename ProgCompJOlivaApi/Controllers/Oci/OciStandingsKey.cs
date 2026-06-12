using System.Text.RegularExpressions;

namespace ProgCompJOlivaApi.Controllers.Oci;

/// <summary>
/// Maps an OCI standings key / seed file name to its (type, year) coordinates. Keys look like
/// <c>regional2022</c>, <c>nacional2023</c>, or <c>clasificatoria2024</c> (the OCI's IOI qualifier —
/// the bare <c>ioi</c> type is reserved for the international IOI). A multi-phase clasificatoria uses
/// a phase suffix — <c>clasificatoria2024-1</c>, <c>clasificatoria2024-2</c> for the phases and
/// <c>clasificatoria2024</c> for the weighted aggregate — all of which parse to the same (type, year)
/// so they group into one edition.
/// </summary>
public static partial class OciStandingsKey
{
    /// <summary>The valid edition types, in display order.</summary>
    public static readonly string[] Types = ["regional", "nacional", "clasificatoria"];

    [GeneratedRegex(@"^(regional|nacional|clasificatoria)(\d{4})(?:-[a-z0-9]+)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex KeyRegex();

    /// <summary>
    /// Normalizes an edition type to its canonical form: lower-cased, with the common aliases
    /// (<c>national</c> → <c>nacional</c>, <c>clasificatorias</c> → <c>clasificatoria</c>) collapsed.
    /// Returns null if it isn't a known type.
    /// </summary>
    public static string? NormalizeType(string type)
    {
        var t = (type ?? "").Trim().ToLowerInvariant();
        t = t switch { "national" => "nacional", "regionals" => "regional", "clasificatorias" => "clasificatoria", _ => t };
        return Types.Contains(t) ? t : null;
    }

    /// <summary>Parses a key such as <c>regional2022</c> into its parts.</summary>
    public static bool TryParse(string key, out string type, out int year)
    {
        type = "";
        year = 0;

        if (string.IsNullOrWhiteSpace(key))
            return false;

        var m = KeyRegex().Match(key.Trim());
        if (!m.Success)
            return false;

        type = m.Groups[1].Value.ToLowerInvariant();
        year = int.Parse(m.Groups[2].Value);
        return true;
    }
}
