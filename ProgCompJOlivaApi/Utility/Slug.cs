using System.Globalization;
using System.Text;

namespace ProgCompJOlivaApi.Utility;

public static class Slug
{
    /// <summary>
    /// Turns an arbitrary string into a URL-friendly slug: lowercase ASCII, accents
    /// stripped, runs of non-alphanumeric characters collapsed to single hyphens.
    /// </summary>
    public static string Generate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Decompose accents (á -> a + ́) and drop the combining marks.
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
            else
                sb.Append('-');
        }

        var collapsed = sb.ToString();

        // Collapse multiple hyphens and trim leading/trailing ones.
        while (collapsed.Contains("--"))
            collapsed = collapsed.Replace("--", "-");

        return collapsed.Trim('-');
    }
}
