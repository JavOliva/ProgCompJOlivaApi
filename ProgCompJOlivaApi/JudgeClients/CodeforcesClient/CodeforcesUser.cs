using System.Text.Json.Serialization;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

public class CodeforcesUser
{
    [JsonPropertyName("handle")] public string Handle { get; set; } = null!;

    [JsonPropertyName("email")] public string? Email { get; set; }

    [JsonPropertyName("vkId")] public string? VkId { get; set; }

    [JsonPropertyName("openId")] public string? OpenId { get; set; }

    [JsonPropertyName("firstName")] public string? FirstName { get; set; }

    [JsonPropertyName("lastName")] public string? LastName { get; set; }

    [JsonPropertyName("country")] public string? Country { get; set; }

    [JsonPropertyName("city")] public string? City { get; set; }

    [JsonPropertyName("organization")] public string? Organization { get; set; }

    [JsonPropertyName("contribution")] public int Contribution { get; set; }

    [JsonPropertyName("rank")] public string? Rank { get; set; }

    [JsonPropertyName("rating")] public int? Rating { get; set; }

    [JsonPropertyName("maxRank")] public string? MaxRank { get; set; }

    [JsonPropertyName("maxRating")] public int? MaxRating { get; set; }

    [JsonPropertyName("lastOnlineTimeSeconds")] public long LastOnlineTimeSeconds { get; set; }

    [JsonPropertyName("registrationTimeSeconds")] public long RegistrationTimeSeconds { get; set; }

    [JsonPropertyName("friendOfCount")] public int FriendOfCount { get; set; }

    [JsonPropertyName("avatar")] public string? Avatar { get; set; }

    [JsonPropertyName("titlePhoto")] public string? TitlePhoto { get; set; }
}