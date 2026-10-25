using System.Collections.Concurrent;
using System.Text.Json;
using DiscordNative.Protocol.Models;
using DiscordNative.Protocol.Rest;

namespace DiscordNative.App.Services;

public static class ServerAssetCacheService
{
    private static readonly ConcurrentDictionary<string, List<DiscordEmoji>> _emojisByGuild = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, List<DiscordSticker>> _stickersByGuild = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> _fetchingEmojis = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, bool> _fetchingStickers = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<List<DiscordEmoji>> EnsureGuildEmojisAsync(string guildId, DiscordRestClient? rest)
    {
        if (string.IsNullOrEmpty(guildId)) return new List<DiscordEmoji>();
        if (_emojisByGuild.TryGetValue(guildId, out var cached) && cached.Count > 0)
        {
            return cached;
        }

        if (rest == null) return new List<DiscordEmoji>();

        if (_fetchingEmojis.TryAdd(guildId, true))
        {
            try
            {
                var emojis = await rest.GetGuildEmojisAsync(guildId).ConfigureAwait(false);
                if (emojis != null && emojis.Count > 0)
                {
                    _emojisByGuild[guildId] = emojis;
                    return emojis;
                }
            }
            catch { }
            finally
            {
                _fetchingEmojis.TryRemove(guildId, out _);
            }
        }

        return _emojisByGuild.TryGetValue(guildId, out var existing) ? existing : new List<DiscordEmoji>();
    }

    public static async Task<List<DiscordSticker>> EnsureGuildStickersAsync(string guildId, DiscordRestClient? rest)
    {
        if (string.IsNullOrEmpty(guildId)) return new List<DiscordSticker>();
        if (_stickersByGuild.TryGetValue(guildId, out var cached) && cached.Count > 0)
        {
            return cached;
        }

        if (rest == null) return new List<DiscordSticker>();

        if (_fetchingStickers.TryAdd(guildId, true))
        {
            try
            {
                var stickers = await rest.GetGuildStickersAsync(guildId).ConfigureAwait(false);
                if (stickers != null && stickers.Count > 0)
                {
                    _stickersByGuild[guildId] = stickers;
                    return stickers;
                }
            }
            catch { }
            finally
            {
                _fetchingStickers.TryRemove(guildId, out _);
            }
        }

        return _stickersByGuild.TryGetValue(guildId, out var existing) ? existing : new List<DiscordSticker>();
    }
}
