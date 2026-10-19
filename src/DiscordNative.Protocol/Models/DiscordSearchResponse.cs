using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordSearchResponse
{
    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("messages")]
    public List<List<DiscordMessage>>? Messages { get; set; }
}
