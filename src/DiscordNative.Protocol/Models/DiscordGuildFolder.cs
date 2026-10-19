using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordGuildFolderData
{
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("color")]
    public long? Color { get; set; }

    [JsonPropertyName("guild_ids")]
    public List<string> GuildIds { get; set; } = new();

    [JsonIgnore]
    public string? IdString => Id?.ToString();

    [JsonIgnore]
    public bool IsFolder => GuildIds.Count > 1 || (!string.IsNullOrEmpty(IdString) && IdString != "null");
}
