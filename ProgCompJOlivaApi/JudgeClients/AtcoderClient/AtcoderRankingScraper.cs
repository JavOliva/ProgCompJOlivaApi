using System.Globalization;
using HtmlAgilityPack;

namespace ProgCompJOlivaApi.JudgeClients.AtcoderClient;

public static class AtcoderRankingScraper
{

    private static readonly HttpClient Http = new();

    private static string GetUrlPage(int page)
    {
        if (page <= 0)
            throw new ArgumentException($"Page url should be positive but was {page}.");
        else if (page == 1)
            return "https://atcoder.jp/ranking/all?f.Country=CL&f.UserScreenName=&f.Affiliation=&f.BirthYearLowerBound=0&f.BirthYearUpperBound=9999&f.RatingLowerBound=0&f.RatingUpperBound=9999&f.HighestRatingLowerBound=0&f.HighestRatingUpperBound=9999&f.CompetitionsLowerBound=0&f.CompetitionsUpperBound=9999&f.WinsLowerBound=0&f.WinsUpperBound=9999&contestType=algo";
        else
            return $"https://atcoder.jp/ranking/all?contestType=algo&f.Affiliation=&f.BirthYearLowerBound=0&f.BirthYearUpperBound=9999&f.CompetitionsLowerBound=0&f.CompetitionsUpperBound=9999&f.Country=CL&f.HighestRatingLowerBound=0&f.HighestRatingUpperBound=9999&f.RatingLowerBound=0&f.RatingUpperBound=9999&f.UserScreenName=&f.WinsLowerBound=0&f.WinsUpperBound=9999&page={page}";
    }

    public static async Task<List<AtcoderUserRankingEntry>> GetUserRankingEntries()
    {
        var page = 1;
        Dictionary<string, AtcoderUserRankingEntry> entries = [];
        while(true)
        {
            try
            {
                var url = GetUrlPage(page);
                var html = await Http.GetStringAsync(url);
                var list = ParseFromHtml(html);

                foreach (var entry in list)
                {
                    entries.TryAdd(entry.Handle, entry);
                }

                if (list.Count < 100)
                    break;
            }
            catch (Exception ex)
            {
                break;
            }
            page++;
        }

        return [.. entries.Values];
    }

    public static List<AtcoderUserRankingEntry> ParseFromHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var table = (document.DocumentNode
            .SelectNodes("//table")
            ?.FirstOrDefault(IsRankingTable)) ?? throw new InvalidOperationException($"Ranking table not found.");

        var tableBody = table.SelectSingleNode("./tbody") ?? throw new InvalidOperationException($"Ranking table does not have a body");

        var result = new List<AtcoderUserRankingEntry>();

        foreach (var row in tableBody.SelectNodes("./tr") ?? Enumerable.Empty<HtmlNode>())
        {
            var cells = row.SelectNodes("./td");
            if (cells == null || cells.Count < 7)
                continue;

            var entry = new AtcoderUserRankingEntry
            {
                CountryRank = ParseCountryRank(cells[0].InnerText),
                GlobalRank = ParseGlobalRank(cells[0].InnerText),
                Handle = ParseHandle(cells[1]),
                CountryCode = ParseCountryCode(cells[1]),
                Affiliation = ParseAffiliation(cells[1]),
                BirthYear = ParseNullableInt(cells[2].InnerText),
                CurrentRating = ParseRequiredInt(cells[3].InnerText),
                HighestRating = ParseRequiredInt(cells[4].InnerText),
                Matches = ParseRequiredInt(cells[5].InnerText),
                Wins = ParseRequiredInt(cells[6].InnerText)
            };

            if (!string.IsNullOrWhiteSpace(entry.Handle))
                result.Add(entry);
        }

        return result;
    }

    private static bool IsRankingTable(HtmlNode table)
    {
        var headerCells = table.SelectNodes("./thead/tr/th");
        if (headerCells == null || headerCells.Count < 7)
            return false;

        var headers = headerCells
            .Select(x => NormalizeSpaces(HtmlEntity.DeEntitize(x.InnerText)))
            .ToList();

        return headers.Any(headers => headers.Contains("Rank", StringComparison.OrdinalIgnoreCase))
            && headers.Any(h => h.Contains("User", StringComparison.OrdinalIgnoreCase))
            && headers.Any(h => h.Contains("Rating", StringComparison.OrdinalIgnoreCase))
            && headers.Any(h => h.Contains("Highest", StringComparison.OrdinalIgnoreCase))
            && headers.Any(h => h.Contains("Match", StringComparison.OrdinalIgnoreCase))
            && headers.Any(h => h.Contains("Win", StringComparison.OrdinalIgnoreCase));
    }

    #region Parse Fields
    private static string ParseHandle(HtmlNode userCell)
    {
        var userLink = userCell
            .SelectNodes(".//a[@href]")
            ?.FirstOrDefault(a =>
            {
                var href = a.GetAttributeValue("href", "");
                return href.Contains("/users/", StringComparison.OrdinalIgnoreCase);
            });

        return NormalizeSpaces(HtmlEntity.DeEntitize(userLink?.InnerText ?? ""));
    }

    private static string? ParseCountryCode(HtmlNode userCell)
    {
        var countryImg = userCell
            .SelectNodes(".//img[@src]")
            ?.FirstOrDefault(img =>
            {
                var src = img.GetAttributeValue("src", "");
                return src.EndsWith("/CL.png", StringComparison.OrdinalIgnoreCase)
                    || src.EndsWith("\\CL.png", StringComparison.OrdinalIgnoreCase)
                    || src.Contains("/CL.png", StringComparison.OrdinalIgnoreCase);
            });

        if (countryImg != null)
            return "CL";

        var firstImg = userCell.SelectSingleNode(".//img[@src]");
        var srcValue = firstImg?.GetAttributeValue("src", "");
        if (string.IsNullOrWhiteSpace(srcValue))
            return null;

        var fileName = Path.GetFileNameWithoutExtension(srcValue);
        if (!string.IsNullOrWhiteSpace(fileName) && fileName.Length == 2)
            return fileName.ToUpperInvariant();

        return null;
    }

    private static string? ParseAffiliation(HtmlNode userCell)
    {
        var affiliationNode = userCell.SelectSingleNode(".//*[contains(@class,'ranking-affiliation')]");
        var text = NormalizeSpaces(HtmlEntity.DeEntitize(affiliationNode?.InnerText ?? ""));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? ParseCountryRank(string rankCellText)
    {
        var text = NormalizeSpaces(rankCellText);
        var start = text.IndexOf('(');
        var end = text.IndexOf(')');

        if (start >= 0 && end > start)
        {
            var inside = text.Substring(start + 1, end - start - 1);
            return ParseNullableInt(inside);
        }

        return null;
    }

    private static int? ParseGlobalRank(string rankCellText)
    {
        var text = NormalizeSpaces(rankCellText);
        var end = text.IndexOf(')');
        if (end >= 0 && end + 1 < text.Length)
        {
            var tail = text[(end + 1)..].Trim();
            return ParseNullableInt(tail);
        }

        return ParseNullableInt(text);
    }

    private static int ParseRequiredInt(string text)
    {
        var value = ParseNullableInt(text);
        if (value == null)
            throw new FormatException($"No se pudo convertir a int: '{text}'");

        return value.Value;
    }

    private static int? ParseNullableInt(string text)
    {
        var clean = NormalizeSpaces(HtmlEntity.DeEntitize(text))
            .Replace(",", "")
            .Trim();

        if (string.IsNullOrWhiteSpace(clean))
            return null;

        return int.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
    #endregion

    private static string NormalizeSpaces(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        return string.Join(" ", text
            .Split(new[] { ' ', '\t', '\r', '\n', '\u00A0' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
