using System.Collections.ObjectModel;
using DiscordNative.Protocol.Models;
using DiscordNative.Protocol.Rest;

namespace DiscordNative.App.Services;

public sealed class StickerItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public int FormatType { get; set; } = 1;
    public string Url { get; set; } = string.Empty;
    public string? GuildId { get; set; }
    public string? GuildName { get; set; }
    public string? PackName { get; set; }
    public bool IsGuildSticker { get; set; }

    public string TooltipText => !string.IsNullOrWhiteSpace(Description)
        ? $"{Name} - {Description}"
        : $"{Name} ({(IsGuildSticker ? (GuildName ?? "Serveur") : (PackName ?? "Discord"))})";
}

public sealed class StickerCategoryGroup
{
    public string Title { get; set; } = string.Empty;
    public string? IconGlyph { get; set; }
    public string? IconUrl { get; set; }
    public bool HasIconUrl => !string.IsNullOrEmpty(IconUrl);
    public List<StickerItemViewModel> Stickers { get; set; } = new();
}

public sealed class StickerService
{
    private static readonly Lazy<StickerService> _instance = new(() => new StickerService());
    public static StickerService Instance => _instance.Value;

    private List<DiscordStickerPack>? _cachedPacks;
    private readonly Dictionary<string, List<DiscordSticker>> _cachedGuildStickers = new();
    private readonly List<StickerItemViewModel> _recentStickers = new();
    private const int MaxRecentStickers = 24;

    public void TrackRecentSticker(StickerItemViewModel sticker)
    {
        lock (_recentStickers)
        {
            _recentStickers.RemoveAll(s => s.Id == sticker.Id);
            _recentStickers.Insert(0, sticker);
            if (_recentStickers.Count > MaxRecentStickers)
            {
                _recentStickers.RemoveAt(_recentStickers.Count - 1);
            }
        }
    }

    public async Task<List<StickerCategoryGroup>> BuildPickerCatalogAsync(
        IEnumerable<DiscordGuild>? guilds,
        string? currentGuildId,
        DiscordRestClient? restClient,
        string? filterQuery = null)
    {
        // 1. Charger les packs Discord si non en cache
        if (_cachedPacks == null && restClient != null)
        {
            try
            {
                _cachedPacks = await restClient.GetStickerPacksAsync();
            }
            catch
            {
                _cachedPacks = new List<DiscordStickerPack>();
            }
        }

        // 2. Charger les stickers du serveur actuel si besoin
        if (!string.IsNullOrEmpty(currentGuildId) && restClient != null && !_cachedGuildStickers.ContainsKey(currentGuildId))
        {
            try
            {
                var serverStickers = await restClient.GetGuildStickersAsync(currentGuildId);
                _cachedGuildStickers[currentGuildId] = serverStickers;
                var currentGuild = guilds?.FirstOrDefault(g => g.Id == currentGuildId);
                if (currentGuild != null && (currentGuild.Stickers == null || currentGuild.Stickers.Count == 0))
                {
                    currentGuild.Stickers = serverStickers;
                }
            }
            catch
            {
                _cachedGuildStickers[currentGuildId] = new List<DiscordSticker>();
            }
        }

        var groups = new List<StickerCategoryGroup>();
        var cleanQuery = filterQuery?.Trim().ToLowerInvariant();
        bool isSearching = !string.IsNullOrWhiteSpace(cleanQuery);

        if (isSearching)
        {
            var searchResults = new List<StickerItemViewModel>();

            // Recherche dans les serveurs
            if (guilds != null)
            {
                foreach (var g in guilds)
                {
                    var stickers = g.Stickers;
                    if ((stickers == null || stickers.Count == 0) && _cachedGuildStickers.TryGetValue(g.Id, out var cached))
                    {
                        stickers = cached;
                    }
                    if (stickers == null) continue;

                    foreach (var s in stickers)
                    {
                        if (MatchesQuery(s, cleanQuery!))
                        {
                            searchResults.Add(FromDiscordSticker(s, g.Id, g.Name, null, true));
                        }
                    }
                }
            }

            // Recherche dans les packs Discord
            if (_cachedPacks != null)
            {
                foreach (var pack in _cachedPacks)
                {
                    foreach (var s in pack.Stickers)
                    {
                        if (MatchesQuery(s, cleanQuery!))
                        {
                            searchResults.Add(FromDiscordSticker(s, null, null, pack.Name, false));
                        }
                    }
                }
            }

            groups.Add(new StickerCategoryGroup
            {
                Title = $"Résultats pour \"{cleanQuery}\" ({searchResults.Count})",
                IconGlyph = "\uE721",
                Stickers = searchResults
            });

            return groups;
        }

        // Mode Navigation : Catégories Discord

        // 1. Récents
        lock (_recentStickers)
        {
            if (_recentStickers.Count > 0)
            {
                groups.Add(new StickerCategoryGroup
                {
                    Title = "Fréquemment utilisés",
                    IconGlyph = "\uE823",
                    Stickers = new List<StickerItemViewModel>(_recentStickers)
                });
            }
        }

        // 2. Serveur actuel
        if (!string.IsNullOrEmpty(currentGuildId) && guilds != null)
        {
            var currentGuild = guilds.FirstOrDefault(g => g.Id == currentGuildId);
            if (currentGuild != null)
            {
                var stickers = currentGuild.Stickers;
                if ((stickers == null || stickers.Count == 0) && _cachedGuildStickers.TryGetValue(currentGuild.Id, out var cached))
                {
                    stickers = cached;
                }

                if (stickers != null && stickers.Count > 0)
                {
                    groups.Add(new StickerCategoryGroup
                    {
                        Title = $"{currentGuild.Name} (Serveur actuel)",
                        IconUrl = currentGuild.IconUrl,
                        IconGlyph = "\uE774",
                        Stickers = stickers.Select(s => FromDiscordSticker(s, currentGuild.Id, currentGuild.Name, null, true)).ToList()
                    });
                }
            }
        }

        // 3. Autres serveurs
        if (guilds != null)
        {
            foreach (var g in guilds)
            {
                if (g.Id == currentGuildId) continue;
                var stickers = g.Stickers;
                if ((stickers == null || stickers.Count == 0) && _cachedGuildStickers.TryGetValue(g.Id, out var cached))
                {
                    stickers = cached;
                }

                if (stickers != null && stickers.Count > 0)
                {
                    groups.Add(new StickerCategoryGroup
                    {
                        Title = g.Name,
                        IconUrl = g.IconUrl,
                        IconGlyph = "\uE774",
                        Stickers = stickers.Select(s => FromDiscordSticker(s, g.Id, g.Name, null, true)).ToList()
                    });
                }
            }
        }

        // 4. Packs de stickers officiels Discord
        if (_cachedPacks != null)
        {
            foreach (var pack in _cachedPacks)
            {
                if (pack.Stickers.Count == 0) continue;
                groups.Add(new StickerCategoryGroup
                {
                    Title = pack.Name,
                    IconGlyph = "\uEB9F",
                    Stickers = pack.Stickers.Select(s => FromDiscordSticker(s, null, null, pack.Name, false)).ToList()
                });
            }
        }

        return groups;
    }

    private static bool MatchesQuery(DiscordSticker s, string query)
    {
        if (s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(s.Tags) && s.Tags.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(s.Description) && s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static StickerItemViewModel FromDiscordSticker(DiscordSticker s, string? guildId, string? guildName, string? packName, bool isGuild)
    {
        return new StickerItemViewModel
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Tags = s.Tags,
            FormatType = s.FormatType,
            Url = s.Url,
            GuildId = guildId,
            GuildName = guildName,
            PackName = packName,
            IsGuildSticker = isGuild
        };
    }
}
