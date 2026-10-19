using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordSticker
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("pack_id")] public string? PackId { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("tags")] public string? Tags { get; set; }
    [JsonPropertyName("asset")] public string? Asset { get; set; }
    [JsonPropertyName("type")] public int Type { get; set; } = 1; // 1 = Standard, 2 = Guild
    [JsonPropertyName("format_type")] public int FormatType { get; set; } = 1; // 1 = PNG, 2 = APNG, 3 = Lottie, 4 = GIF
    [JsonPropertyName("available")] public bool Available { get; set; } = true;
    [JsonPropertyName("guild_id")] public string? GuildId { get; set; }
    [JsonPropertyName("sort_value")] public int? SortValue { get; set; }

    [JsonIgnore]
    public string Url => $"https://media.discordapp.net/stickers/{Id}.{(FormatType == 4 ? "gif" : "png")}?size=160";

    [JsonIgnore]
    public string PreviewUrl => $"https://media.discordapp.net/stickers/{Id}.png?size=160";
}

public sealed class DiscordStickerPack
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("stickers")] public List<DiscordSticker> Stickers { get; set; } = new();
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("sku_id")] public string? SkuId { get; set; }
    [JsonPropertyName("summary")] public string? Summary { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("banner_asset_id")] public string? BannerAssetId { get; set; }

    [JsonIgnore]
    public string? BannerUrl => !string.IsNullOrEmpty(BannerAssetId)
        ? $"https://cdn.discordapp.com/app-assets/710982414301790216/store/{BannerAssetId}.png?size=256"
        : null;
}

public sealed class DiscordStickerPacksResponse
{
    [JsonPropertyName("sticker_packs")]
    public List<DiscordStickerPack> StickerPacks { get; set; } = new();
}
