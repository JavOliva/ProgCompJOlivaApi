using System.Text.Json.Serialization;

namespace ProgCompJOlivaApi.JudgeClients.CodeforcesClient;

public sealed class CodeforcesApiResponse<T>
{
    [JsonPropertyName("status")] [JsonConverter(typeof(JsonStringEnumConverter))] public CodeforcesResponseStatus Status { get; set; }

    [JsonPropertyName("comment")] public string? Comment { get; set; }

    [JsonPropertyName("result")] public T? Result { get; set; }
}