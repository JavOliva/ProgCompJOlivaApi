namespace ProgCompJOlivaApi.Controllers.Oci.Dtos;

/// <summary>
/// Standings for an OCI (Olimpiada Chilena de Informática) edition. Being a school olympiad, there is
/// no ICPC-style penalty: tasks have subtasks, so each task yields a partial score (0..maxScore) and
/// the total (<see cref="OciStandingsRow.Global"/>) is the sum across tasks. Rows are ordered as in
/// the source document.
/// </summary>
public class OciStandings
{
    public string Contest { get; set; } = "";

    /// <summary>Edition type: <c>regional</c>, <c>nacional</c>, or <c>clasificatoria</c> (the OCI's IOI qualifier).</summary>
    public string Type { get; set; } = "";

    public int Year { get; set; }

    /// <summary>
    /// Phase number for a multi-phase edition (only IOI clasificatorias have phases), or null for a
    /// single standings (regional, nacional, single-phase IOI) and for the weighted aggregate.
    /// </summary>
    public int? Phase { get; set; }

    /// <summary>
    /// True when this is the weighted aggregate across the phases of a multi-phase IOI clasificatoria.
    /// The qualification flag (<see cref="OciStandingsRow.Qualified"/>) lives on this standings.
    /// </summary>
    public bool Weighted { get; set; }

    /// <summary>This phase's weight in the weighted aggregate (e.g. 0.4), when applicable. Informational.</summary>
    public double? Weight { get; set; }

    /// <summary>
    /// The table columns: tasks for a regional / nacional / IOI-phase standings, or the phases for a
    /// weighted IOI aggregate.
    /// </summary>
    public List<OciStandingsProblem> Problems { get; set; } = [];

    /// <summary>One row per participant, in document order.</summary>
    public List<OciStandingsRow> Rows { get; set; } = [];

    /// <summary>
    /// The individual phase standings, only populated on the weighted aggregate of a multi-phase IOI
    /// clasificatoria (ordered by phase number). Null/absent for regional, nacional and single-phase
    /// editions.
    /// </summary>
    public List<OciStandings>? Phases { get; set; }
}

public class OciStandingsProblem
{
    public string Name { get; set; } = "";

    /// <summary>Maximum attainable score for this task (sum of its subtasks).</summary>
    public int MaxScore { get; set; } = 100;
}

public class OciStandingsRow
{
    /// <summary>Official rank in the document, or null for an entry shown without a rank (hors-concours).</summary>
    public int? Rank { get; set; }

    /// <summary>Venue/seat where the participant competed (e.g. university), or empty if not given.</summary>
    public string Sede { get; set; } = "";

    /// <summary>The participant's OCI username/handle as printed in the document.</summary>
    public string Username { get; set; } = "";

    /// <summary>The participant's full real name as printed in the document.</summary>
    public string User { get; set; } = "";

    /// <summary>Per-task scores, aligned with <see cref="OciStandings.Problems"/>.</summary>
    public List<int> Scores { get; set; } = [];

    /// <summary>Total score: sum of <see cref="Scores"/>, or the weighted total on a weighted aggregate.</summary>
    public int Global { get; set; }

    /// <summary>
    /// Whether the participant advanced to the next stage. Meaning depends on the standings: regional →
    /// classified to the Nacional; nacional → classified to the training camp (where the IOI selection
    /// happens); IOI (single-phase or the weighted aggregate) → classified to the IOI.
    /// </summary>
    public bool Qualified { get; set; }

    /// <summary>
    /// Medal won at a Nacional: <c>oro</c>, <c>plata</c>, <c>bronce</c>, or null (no medal / not a
    /// nacional). Independent of <see cref="Qualified"/> — a medalist may or may not advance.
    /// </summary>
    public string? Medal { get; set; }

    /// <summary>Whether <see cref="Username"/>/<see cref="User"/> was matched to a registered platform user.</summary>
    public bool Registered { get; set; }

    /// <summary>The matched user's canonical real name, or null if unmatched.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The matched user's platform nickname, or null if unmatched.</summary>
    public string? Nickname { get; set; }

    /// <summary>The matched user's Codeforces rating, so the frontend can color the name. Null if unmatched.</summary>
    public int? Rating { get; set; }
}
