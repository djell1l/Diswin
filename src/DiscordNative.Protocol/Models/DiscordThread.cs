using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordActiveThreadsResponse
{
    [JsonPropertyName("threads")]
    public List<DiscordChannel>? Threads { get; set; }

    [JsonPropertyName("members")]
    public List<DiscordThreadMember>? Members { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

public sealed class DiscordThreadMember
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("join_timestamp")]
    public DateTimeOffset? JoinTimestamp { get; set; }
}

public sealed class DiscordThreadMetadata
{
    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("auto_archive_duration")]
    public int AutoArchiveDuration { get; set; }

    [JsonPropertyName("archive_timestamp")]
    public DateTimeOffset? ArchiveTimestamp { get; set; }

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }
}
