using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol;

public sealed class DiscordApplicationInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    [JsonPropertyOrder(1)]
    public string? Icon { get; set; }

    [JsonPropertyName("icon_hash")]
    public string? IconHash { get; set; }

    [JsonPropertyName("cover_image")]
    public string? CoverImage { get; set; }

    [JsonPropertyName("cover_image_hash")]
    public string? CoverImageHash { get; set; }

    public string? EffectiveIconHash => !string.IsNullOrEmpty(Icon) ? Icon : IconHash;
    public string? EffectiveCoverHash => !string.IsNullOrEmpty(CoverImage) ? CoverImage : CoverImageHash;

    public string? IconUrl => !string.IsNullOrEmpty(EffectiveIconHash)
        ? $"https://cdn.discordapp.com/app-icons/{Id}/{EffectiveIconHash}.png?size=128"
        : null;

    public string? CoverUrl => !string.IsNullOrEmpty(EffectiveCoverHash)
        ? $"https://cdn.discordapp.com/app-icons/{Id}/{EffectiveCoverHash}.png?size=256"
        : null;
}

public static class DiscordApplicationCache
{
    private static readonly ConcurrentDictionary<string, DiscordApplicationInfo> _appsById = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DiscordApplicationInfo> _appsByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient _httpClient = new();
    private static bool _initialized;
    private static readonly object _initLock = new();

    public static void Initialize()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            _initialized = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/v9/applications/detectable");
                    req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    using var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
                    if (res.IsSuccessStatusCode)
                    {
                        using var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                                var name = item.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                                var iconHash = item.TryGetProperty("icon_hash", out var iProp) ? iProp.GetString() : null;
                                var coverHash = item.TryGetProperty("cover_image_hash", out var cProp) ? cProp.GetString() : null;

                                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;

                                var appInfo = new DiscordApplicationInfo
                                {
                                    Id = id,
                                    Name = name,
                                    IconHash = iconHash,
                                    CoverImageHash = coverHash
                                };

                                _appsById[id] = appInfo;
                                if (!_appsByName.ContainsKey(name))
                                {
                                    _appsByName[name] = appInfo;
                                }

                                if (item.TryGetProperty("aliases", out var aliases) && aliases.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var aliasEl in aliases.EnumerateArray())
                                    {
                                        var alias = aliasEl.GetString();
                                        if (!string.IsNullOrEmpty(alias) && !_appsByName.ContainsKey(alias))
                                        {
                                            _appsByName[alias] = appInfo;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            });
        }
    }

    public static DiscordApplicationInfo? FindApplication(string? appId, string? appName)
    {
        if (!string.IsNullOrEmpty(appId) && _appsById.TryGetValue(appId, out var infoById))
        {
            return infoById;
        }

        if (!string.IsNullOrEmpty(appName))
        {
            if (_appsByName.TryGetValue(appName, out var infoByName))
            {
                return infoByName;
            }

            // Tentative par préfixe / casse tolérante
            var trimmed = appName.Trim();
            foreach (var kvp in _appsByName)
            {
                if (string.Equals(kvp.Key, trimmed, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }

        return null;
    }

    public static async Task<DiscordApplicationInfo?> FetchApplicationRpcAsync(string appId)
    {
        if (string.IsNullOrEmpty(appId)) return null;
        if (_appsById.TryGetValue(appId, out var cached) && !string.IsNullOrEmpty(cached.EffectiveIconHash))
        {
            return cached;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v9/oauth2/applications/{appId}/rpc");
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            using var res = await _httpClient.SendAsync(req).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                var app = JsonSerializer.Deserialize<DiscordApplicationInfo>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (app != null && !string.IsNullOrEmpty(app.Id))
                {
                    _appsById[app.Id] = app;
                    if (!string.IsNullOrEmpty(app.Name))
                    {
                        _appsByName[app.Name] = app;
                    }
                    return app;
                }
            }
        }
        catch { }

        return null;
    }
}
