using System.Globalization;
using System.Text;
using ProgCompJOlivaApi.Controllers.Oci.Dtos;

namespace ProgCompJOlivaApi.Controllers.Oci;

/// <summary>
/// Parses the OCI standings CSV format shared by the admin upload endpoint and the
/// <c>SeedData/oci-standings/*.csv</c> seeder.
/// </summary>
public static class OciCsvParser
{
    /// <summary>Human-readable default contest name when none is provided.</summary>
    public static string DefaultContestName(string type, int year, int? phase, bool weighted) => type switch
    {
        "clasificatoria" when phase.HasValue => $"OCI Clasificatoria IOI {year} - Fase {phase}",
        "clasificatoria" when weighted => $"OCI Clasificatoria IOI {year} - Ponderado",
        "clasificatoria" => $"OCI Clasificatoria IOI {year}",
        "nacional" => $"Nacional {year}",
        _ => $"Regional {year}",
    };

    /// <summary>
    /// Parses an OCI CSV into standings. Columns are identified by their header <b>name</b>, so they
    /// can come in any order and the optional ones can be absent: <c>Nombre</c> (required) and
    /// <c>TOTAL</c>/<c>Global</c> (required); <c>Sede</c>/<c>Región</c>, <c>Clasifica</c> and
    /// <c>Medalla</c> are optional; a <c>#</c>/rank column is ignored; every other column is a task.
    /// A phase CSV typically has neither Sede, Clasifica nor Medalla. Ranks are recomputed by total desc.
    /// </summary>
    public static OciStandings Parse(string content, string type, int year, string contestName)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count < 2)
            throw new FormatException("Expected a header row and at least one data row.");

        var header = ParseCsvLine(lines[0]);

        int sedeIdx = -1, nameIdx = -1, totalIdx = -1, clasIdx = -1, medalIdx = -1;
        var taskIdx = new List<int>();
        for (var i = 0; i < header.Count; i++)
        {
            var h = Norm(header[i]);
            if (h is "sede" or "region" or "team" or "colegio" or "establecimiento") sedeIdx = i;
            else if (h is "nombre" or "name" or "participante" or "user") nameIdx = i;
            else if (h is "total" or "global") totalIdx = i;
            else if (h.StartsWith("clasif") || h == "qualified") clasIdx = i;
            else if (h is "medalla" or "medallas" or "medal") medalIdx = i;
            else if (h is "#" or "rank" or "puesto" or "lugar" or "n") { /* rank column — ignored */ }
            else taskIdx.Add(i);
        }

        if (nameIdx < 0)
            throw new FormatException("Missing a 'Nombre' column.");
        if (totalIdx < 0)
            throw new FormatException("Missing a 'TOTAL' (or 'Global') column.");
        if (taskIdx.Count == 0)
            throw new FormatException("No task columns found (every column except Nombre/Sede/TOTAL/Clasifica/Medalla/# is a task).");

        var standings = new OciStandings
        {
            Contest = contestName,
            Type = type,
            Year = year,
            Problems = taskIdx.Select(i => new OciStandingsProblem { Name = header[i].Trim(), MaxScore = 100 }).ToList()
        };

        static string Cell(List<string> f, int idx) => idx >= 0 && idx < f.Count ? f[idx].Trim() : "";

        var rows = new List<OciStandingsRow>();
        foreach (var line in lines.Skip(1))
        {
            var f = ParseCsvLine(line);
            var name = Cell(f, nameIdx);
            if (name.Length == 0)
                continue;

            rows.Add(new OciStandingsRow
            {
                Sede = Cell(f, sedeIdx),
                Username = "",
                User = name,
                Scores = taskIdx.Select(i => ParseScore(Cell(f, i))).ToList(),
                Global = ParseScore(Cell(f, totalIdx)),
                Qualified = clasIdx >= 0 && IsTruthy(Cell(f, clasIdx)),
                Medal = medalIdx >= 0 ? ParseMedal(Cell(f, medalIdx)) : null
            });
        }

        rows = rows.OrderByDescending(r => r.Global).ToList();
        for (var i = 0; i < rows.Count; i++)
            rows[i].Rank = i + 1;
        standings.Rows = rows;
        return standings;
    }

    /// <summary>Lower-cases and strips accents from a header cell, for accent-insensitive matching.</summary>
    private static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Normalizes a medal cell (<c>ORO</c>/<c>PLATA</c>/<c>BRONCE</c>) to lowercase, or null (NA/empty).</summary>
    private static string? ParseMedal(string s)
        => s.Trim().ToLowerInvariant() switch
        {
            "oro" or "gold" => "oro",
            "plata" or "silver" => "plata",
            "bronce" or "bronze" => "bronce",
            _ => null,
        };

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static double ParseScore(string s)
    {
        s = s.Trim();
        // Fractional scores are kept as-is (e.g. weighted clasificatoria totals like 214.7).
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return 0;
    }

    private static bool IsTruthy(string s)
        => s.Trim().ToLowerInvariant() is "1" or "true" or "si" or "sí" or "x" or "yes" or "y" or "verdadero";
}
