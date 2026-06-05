namespace ProgCompJOlivaApi.JudgeClients.AtcoderClient;

public class AtcoderUserRankingEntry
{
    public string Handle { get; set; } = "";

    public int CurrentRating { get; set; }

    public int HighestRating { get; set; }

    public int Matches { get; set; }

    public int Wins { get; set; }

    public string? CountryCode { get; set; }

    public string? Affiliation { get; set; }

    public int? BirthYear { get; set; }

    public int? GlobalRank { get; set; }

    public int? CountryRank { get; set; }
}
