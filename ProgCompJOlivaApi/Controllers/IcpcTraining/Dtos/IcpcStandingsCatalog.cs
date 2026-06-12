namespace ProgCompJOlivaApi.Controllers.IcpcTraining.Dtos;

/// <summary>
/// Catalog of the available standings, grouped org → year → phase, so the frontend can build its
/// "selectivos" navigation without hardcoding which contests exist.
/// </summary>
public class IcpcStandingsCatalogOrg
{
    /// <summary>Normalized organization key (e.g. <c>usm</c>, <c>uchile</c>).</summary>
    public string Org { get; set; } = "";

    public List<IcpcStandingsCatalogYear> Years { get; set; } = [];
}

public class IcpcStandingsCatalogYear
{
    public int Year { get; set; }

    /// <summary>One entry per phase ("fase"), ordered by phase number.</summary>
    public List<IcpcStandingsCatalogFase> Fases { get; set; } = [];
}

public class IcpcStandingsCatalogFase
{
    public int Fase { get; set; }

    /// <summary>Storage key, usable with <c>GET /api/icpc-standings/{key}</c>.</summary>
    public string Key { get; set; } = "";

    /// <summary>Human-readable contest name from the <c>.dat</c> file.</summary>
    public string Contest { get; set; } = "";
}
