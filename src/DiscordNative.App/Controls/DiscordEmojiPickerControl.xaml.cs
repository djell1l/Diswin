using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DiscordNative.App.Services;
using DiscordNative.Protocol.Models;

namespace DiscordNative.App.Controls;

public sealed partial class DiscordEmojiPickerControl : UserControl
{
    public ObservableCollection<EmojiCategoryGroup> Groups { get; } = new();

    private IEnumerable<DiscordGuild>? _guilds;
    private string? _currentGuildId;

    public event Action<EmojiItemViewModel>? EmojiSelected;

    public DiscordEmojiPickerControl()
    {
        InitializeComponent();
    }

    public void Initialize(IEnumerable<DiscordGuild>? guilds, string? currentGuildId)
    {
        _guilds = guilds;
        _currentGuildId = currentGuildId;
        SearchTextBox.Text = string.Empty;
        ReloadCatalog(null);
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
