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

    [JsonPropertyName("media_formats")]
    public Dictionary<string, DiscordGifFormat>? MediaFormats { get; set; }

    /// <summary>
    /// URL directe du fichier GIF animé pour l'affichage fluide et animé dans le sélecteur.
    /// Priorise toujours les fichiers réels terminant par .gif (Klipy, Tenor, Giphy) afin que WinUI 3 les anime automatiquement.
    /// </summary>
    [JsonIgnore]
    public string PreviewUrl
    {
        get
        {
            // 1. Si Src est un .gif valide, c'est le format animé direct le plus fiable
            if (!string.IsNullOrEmpty(Src) && Src.Contains(".gif", StringComparison.OrdinalIgnoreCase))
                return Src;

            // 2. Si Preview est un .gif animé direct
            if (!string.IsNullOrEmpty(Preview) && Preview.Contains(".gif", StringComparison.OrdinalIgnoreCase))
                return Preview;

            // 3. Format Tenor v2 / Discord media_formats
            if (MediaFormats != null)
            {
                if (MediaFormats.TryGetValue("gif", out var f) && !string.IsNullOrEmpty(f?.Url)) return f.Url;
                if (MediaFormats.TryGetValue("tinygif", out var tf) && !string.IsNullOrEmpty(tf?.Url)) return tf.Url;
                if (MediaFormats.TryGetValue("nanogif", out var nf) && !string.IsNullOrEmpty(nf?.Url)) return nf.Url;
                if (MediaFormats.TryGetValue("mediumgif", out var mf) && !string.IsNullOrEmpty(mf?.Url)) return mf.Url;
            }

            // 4. Si GifSrc est un .gif
            if (!string.IsNullOrEmpty(GifSrc) && GifSrc.Contains(".gif", StringComparison.OrdinalIgnoreCase))
                return GifSrc;

            // 5. Fallback par défaut
            return !string.IsNullOrEmpty(Src) ? Src
                : (!string.IsNullOrEmpty(Preview) ? Preview
                : (!string.IsNullOrEmpty(GifSrc) ? GifSrc : Url));
        }
    }

    /// <summary>
    /// URL envoyée dans le salon lors du clic sur le GIF.
    /// Discord expand nativement les URLs Klipy et Tenor en embeds médias complets.
    /// </summary>
    [JsonIgnore]
    public string MediaSendUrl
    {
        get
        {
            if (!string.IsNullOrEmpty(Url) && (Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                return Url;

            return PreviewUrl;
        }
    }
}

public sealed class DiscordGifFormat
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

