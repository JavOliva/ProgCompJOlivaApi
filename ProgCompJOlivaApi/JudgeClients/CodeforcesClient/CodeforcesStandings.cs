using System.Text.Json.Serialization;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

public sealed class CodeforcesStandings
{
    [JsonPropertyName("contest")] public CodeforcesContest? Contest { get; set; }

    [JsonPropertyName("problems")] public List<CodeforcesContestProblem> Problems { get; set; } = [];

    [JsonPropertyName("rows")] public List<CodeforcesRanklistRow> Rows { get; set; } = [];
}

public sealed class CodeforcesContest
{
    [JsonPropertyName("id")] public long Id { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class CodeforcesContestProblem
{
    [JsonPropertyName("index")] public string Index { get; set; } = null!;

    [JsonPropertyName("name")] public string Name { get; set; } = null!;

    [JsonPropertyName("rating")] public int? Rating { get; set; }

    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
}

public sealed class CodeforcesRanklistRow
{
    [JsonPropertyName("party")] public CodeforcesParty Party { get; set; } = new();

    [JsonPropertyName("problemResults")] public List<CodeforcesProblemResult> ProblemResults { get; set; } = [];
}

public sealed class CodeforcesParty
{
    [JsonPropertyName("members")] public List<CodeforcesMember> Members { get; set; } = [];

    [JsonPropertyName("participantType")] public string? ParticipantType { get; set; }
}

public sealed class CodeforcesMember
{
    [JsonPropertyName("handle")] public string Handle { get; set; } = null!;
}

public sealed class CodeforcesProblemResult
{
    [JsonPropertyName("points")] public double Points { get; set; }

    [JsonPropertyName("rejectedAttemptCount")] public int? RejectedAttemptCount { get; set; }

    [JsonPropertyName("bestSubmissionTimeSeconds")] public long? BestSubmissionTimeSeconds { get; set; }

    /// <summary>Accepted at least once: positive points or a recorded best-submission time.</summary>
    public bool Solved => Points > 0 || BestSubmissionTimeSeconds.HasValue;
}
