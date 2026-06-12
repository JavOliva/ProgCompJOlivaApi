namespace ProgCompJOlivaApi.Controllers.Oci.Dtos;

/// <summary>
/// Catalog of the available OCI standings, grouped by type (regional / nacional / ioi) so the
/// frontend can discover which editions exist without hardcoding them.
/// </summary>
public class OciStandingsCatalogType
{
    /// <summary>Edition type: <c>regional</c>, <c>nacional</c>, or <c>clasificatoria</c>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Editions of this type, ordered by year.</summary>
    public List<OciStandingsCatalogEdition> Editions { get; set; } = [];
}

public class OciStandingsCatalogEdition
{
    public int Year { get; set; }

    /// <summary>Base key for the edition (<c>{type}{year}</c>); fetch it via <c>GET /{type}/{year}</c>.</summary>
    public string Key { get; set; } = "";

    /// <summary>Human-readable contest name (the weighted aggregate's name for a multi-phase IOI).</summary>
    public string Contest { get; set; } = "";

    /// <summary>Number of phases (nonzero only for multi-phase IOI clasificatorias; 0 otherwise).</summary>
    public int Phases { get; set; }
}
