using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DiscordNative.App.Services;
using DiscordNative.Protocol.Models;
using DiscordNative.Protocol.Rest;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DiscordNative.App.Controls;

public sealed partial class DiscordEmojiPickerControl : UserControl
{
    private IEnumerable<DiscordGuild>? _guilds;
    private string? _currentGuildId;
    public ObservableCollection<EmojiCategoryGroup> Groups { get; } = new();

    public event Action<EmojiItemViewModel>? EmojiSelected;

    public DiscordEmojiPickerControl()
    {
        InitializeComponent();
    }

    public void Initialize(IEnumerable<DiscordGuild>? guilds, string? currentGuildId)
    {
        Initialize(guilds, currentGuildId, null);
    }

    public void Initialize(IEnumerable<DiscordGuild>? guilds, string? currentGuildId, DiscordRestClient? restClient)
    {
        _guilds = guilds;
        _currentGuildId = currentGuildId;
        SearchTextBox.Text = string.Empty;
        ReloadCatalog(null);

        if (!string.IsNullOrEmpty(currentGuildId) && restClient != null)
        {
            _ = Task.Run(async () =>
            {
                var emojis = await ServerAssetCacheService.EnsureGuildEmojisAsync(currentGuildId, restClient);
                if (emojis != null && emojis.Count > 0)
                {
                    var g = _guilds?.FirstOrDefault(x => x.Id == currentGuildId);
                    if (g != null && (g.Emojis == null || g.Emojis.Count == 0))
                    {
                        foreach (var em in emojis)
                        {
                            em.GuildId = g.Id;
                            em.GuildName = g.Name;
                            em.GuildIconUrl = g.IconUrl;
                        }
                        g.Emojis = emojis;
                    }
                    Protocol.App.RunOnUiThread(() => ReloadCatalog(SearchTextBox.Text));
                }
            });
        }
    }

    private void ReloadCatalog(string? query)
    {
        Groups.Clear();
        var catalog = EmojiService.Instance.BuildPickerCatalog(_guilds, _currentGuildId, query);
        foreach (var group in catalog)
        {
            Groups.Add(group);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ReloadCatalog(SearchTextBox.Text);
    }

    private void OnEmojiItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EmojiItemViewModel emoji)
        {
            EmojiService.Instance.TrackRecentEmoji(emoji);
            EmojiSelected?.Invoke(emoji);
        }
    }

    private void OnCategoryNavClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string catTitle })
        {
            // Category navigation
        }
    }
}
