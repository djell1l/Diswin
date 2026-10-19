using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordGifItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("gif_src")]
    public string? GifSrc { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("preview")]
    public string? Preview { get; set; }

    [JsonIgnore]
    public string PreviewUrl => !string.IsNullOrEmpty(GifSrc) ? GifSrc : (!string.IsNullOrEmpty(Preview) ? Preview : (Src ?? Url));

    [JsonIgnore]
    public string MediaSendUrl => !string.IsNullOrEmpty(Url) ? Url : (GifSrc ?? Src ?? string.Empty);
}
