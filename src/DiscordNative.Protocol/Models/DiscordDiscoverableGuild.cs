using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordDiscoverableGuild
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("splash")]
    public string? Splash { get; set; }

    [JsonPropertyName("banner")]
    public string? Banner { get; set; }

    [JsonPropertyName("approximate_member_count")]
    public int? ApproximateMemberCount { get; set; }

    [JsonPropertyName("approximate_presence_count")]
    public int? ApproximatePresenceCount { get; set; }

    [JsonPropertyName("vanity_url_code")]
    public string? VanityUrlCode { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; set; }

    [JsonIgnore]
    public string? Category { get; set; }

    [JsonIgnore]
    public string MemberCountText => ApproximateMemberCount.HasValue
        ? $"{ApproximateMemberCount.Value:N0} Membres"
        : "Communauté publique";

    [JsonIgnore]
    public string PresenceCountText => ApproximatePresenceCount.HasValue
        ? $"{ApproximatePresenceCount.Value:N0} En ligne"
        : "";

    [JsonIgnore]
    public string? IconUrl => !string.IsNullOrEmpty(Icon)
        ? $"https://cdn.discordapp.com/icons/{Id}/{Icon}.png?size=128"
        : null;

    [JsonIgnore]
    public bool HasIcon => !string.IsNullOrEmpty(IconUrl);

    [JsonIgnore]
    public string? BannerUrl => !string.IsNullOrEmpty(Banner)
        ? $"https://cdn.discordapp.com/banners/{Id}/{Banner}.png?size=512"
        : (!string.IsNullOrEmpty(Splash)
            ? $"https://cdn.discordapp.com/discovery-splashes/{Id}/{Splash}.png?size=512"
            : null);

    [JsonIgnore]
    public bool HasBanner => !string.IsNullOrEmpty(BannerUrl);

    [JsonIgnore]
    public bool IsVerified => Features != null && Features.Contains("VERIFIED");

    [JsonIgnore]
    public bool IsPartner => Features != null && Features.Contains("PARTNER");

    [JsonIgnore]
    public bool HasBadge => IsVerified || IsPartner;

    [JsonIgnore]
    public string BadgeText => IsVerified ? "Vérifié" : (IsPartner ? "Partenaire" : "");

    [JsonIgnore]
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "?";
            var words = Name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2
                ? $"{words[0][0]}{words[1][0]}".ToUpperInvariant()
                : (Name.Length >= 2 ? Name[..2].ToUpperInvariant() : Name.ToUpperInvariant());
        }
    }
}

public sealed class DiscordDiscoverableGuildsResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("guilds")]
    public List<DiscordDiscoverableGuild>? Guilds { get; set; }
}
