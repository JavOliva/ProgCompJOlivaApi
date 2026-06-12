using System.Globalization;
using System.Text;
using ProgCompJOlivaApi.Controllers.IcpcTraining.Dtos;

namespace ProgCompJOlivaApi.Controllers.IcpcTraining;

/// <summary>
/// Parses a contest <c>.dat</c> file (the DOMjudge/ICPC-tools format) and computes ICPC standings.
///
/// Lines: <c>@contest "name"</c>, <c>@contlen minutes</c>, <c>@p label,name,penalty,color</c>,
/// <c>@t id,grp,cat,"name"</c>, <c>@s teamId,problem,attempt,timeSeconds,verdict</c>.
///
/// Scoring (standard ICPC): a problem is solved on the first <c>OK</c>; penalty for a solved
/// problem = solveMinutes + penaltyPerWrong × (failed submissions before the AC). Failed =
/// <c>WA</c>/<c>TL</c>/<c>RT</c>/<c>RJ</c>. Compile errors (<c>CE</c>) are free (counted as an attempt
/// but not penalized). Pending/unknown verdicts (<c>??</c>) and submissions to unknown problems are
/// ignored. Team names may be quoted or bare.
/// </summary>
public static class DatStandingsParser
{
    private static readonly HashSet<string> FailedVerdicts = ["WA", "TL", "RT", "RJ"];
    private static readonly HashSet<string> KnownVerdicts = ["OK", "WA", "TL", "RT", "RJ", "CE"];

    private record Submission(int TeamId, string Problem, int TimeSeconds, string Verdict);

    /// <summary>
    /// Decodes raw <c>.dat</c> bytes to text. Tries UTF-8 strictly first; if the bytes aren't valid
    /// UTF-8 (some files store accented contest names as Latin-1, e.g. <c>0xF3</c> for "ó"), falls
    /// back to Latin-1 so accents survive instead of becoming replacement characters.
    /// </summary>
    public static string DecodeBytes(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>Convenience overload: decode then parse.</summary>
    public static IcpcStandings Parse(byte[] bytes)
        => Parse(DecodeBytes(bytes));

    public static IcpcStandings Parse(string content)
    {
        var standings = new IcpcStandings();
        var problemLabels = new List<string>();
        var problemNames = new Dictionary<string, string>();
        var problemPenalty = new Dictionary<string, int>();
        var teamNames = new Dictionary<int, string>();
        var teamOrder = new List<int>();
        var submissions = new List<Submission>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("@contest"))
            {
                var first = line.IndexOf('"');
                var last = line.LastIndexOf('"');
                if (first >= 0 && last > first)
                    standings.Contest = line.Substring(first + 1, last - first - 1);
            }
            else if (line.StartsWith("@contlen"))
            {
                standings.ContestLengthMinutes = ParseInt(line["@contlen".Length..]);
            }
            else if (line.StartsWith("@p "))
            {
                // label,name,penalty,color  (name may contain commas)
                var parts = line[3..].Split(',');
                if (parts.Length >= 4)
                {
                    var label = parts[0].Trim();
                    var penalty = ParseInt(parts[^2]);
                    var name = string.Join(",", parts[1..^2]).Trim();
                    if (!problemNames.ContainsKey(label))
                        problemLabels.Add(label);
                    problemNames[label] = name;
                    problemPenalty[label] = penalty;
                }
            }
            else if (line.StartsWith("@t "))
            {
                // id,grp,cat,name  — name may be quoted ("abner_vidal") or bare (dariasc).
                var parts = line[3..].Split(',', 4);
                if (parts.Length < 4)
                    continue;
                var id = ParseInt(parts[0]);
                var name = parts[3].Trim();
                if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
                    name = name[1..^1];
                if (!teamNames.ContainsKey(id))
                    teamOrder.Add(id);
                teamNames[id] = name;
            }
            else if (line.StartsWith("@s "))
            {
                // teamId,problem,attempt,timeSeconds,verdict
                var parts = line[3..].Split(',');
                if (parts.Length >= 5)
                    submissions.Add(new Submission(ParseInt(parts[0]), parts[1].Trim(), ParseInt(parts[3]), parts[4].Trim()));
            }
        }

        standings.PenaltyPerWrong = problemPenalty.Values.Count > 0 ? problemPenalty.Values.First() : 20;
        standings.Problems = problemLabels
            .Select(l => new IcpcStandingsProblem { Label = l, Name = problemNames.GetValueOrDefault(l, l) })
            .ToList();

        var labelSet = problemLabels.ToHashSet();

        // (teamId, problem) -> submissions in time order
        var byTeamProblem = submissions
            .Where(s => labelSet.Contains(s.Problem) && KnownVerdicts.Contains(s.Verdict))
            .GroupBy(s => (s.TeamId, s.Problem))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.TimeSeconds).ToList());

        var rows = new List<IcpcStandingsRow>();

        foreach (var teamId in teamOrder)
        {
            var row = new IcpcStandingsRow { TeamId = teamId, Name = teamNames.GetValueOrDefault(teamId, teamId.ToString()) };
            var totalSolved = 0;
            var totalPenalty = 0;

            foreach (var label in problemLabels)
            {
                var cell = new IcpcStandingsCell { Label = label };

                if (byTeamProblem.TryGetValue((teamId, label), out var subs))
                {
                    cell.Attempted = true;
                    var failed = 0;

                    foreach (var s in subs)
                    {
                        if (s.Verdict == "OK")
                        {
                            cell.Solved = true;
                            cell.SolveTimeMinutes = s.TimeSeconds / 60;
                            break; // ICPC: ignore submissions after the first accepted one
                        }
                        if (FailedVerdicts.Contains(s.Verdict))
                            failed++;
                        // CE: counts as an attempt but is not penalized
                    }

                    cell.FailedAttempts = failed;

                    if (cell.Solved)
                    {
                        cell.Penalty = cell.SolveTimeMinutes!.Value + problemPenalty.GetValueOrDefault(label, 20) * failed;
                        totalSolved++;
                        totalPenalty += cell.Penalty;
                    }
                }

                row.Problems.Add(cell);
            }

            row.Solved = totalSolved;
            row.Penalty = totalPenalty;
            rows.Add(row);
        }

        rows = rows
            .OrderByDescending(r => r.Solved)
            .ThenBy(r => r.Penalty)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var i = 0; i < rows.Count; i++)
            rows[i].Rank = i + 1;

        standings.Rows = rows;
        return standings;
    }

    private static int ParseInt(string s)
        => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
