using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DiscordNative.App.Services;
using DiscordNative.Protocol.Models;
using DiscordNative.Protocol.Rest;

namespace DiscordNative.App.Controls;

public sealed partial class DiscordStickerPickerControl : UserControl
{
    public ObservableCollection<StickerCategoryGroup> Groups { get; } = new();

    private IEnumerable<DiscordGuild>? _guilds;
    private string? _currentGuildId;
    private DiscordRestClient? _restClient;

    public event Action<StickerItemViewModel>? StickerSelected;

    public DiscordStickerPickerControl()
    {
        InitializeComponent();
    }

    public async void Initialize(IEnumerable<DiscordGuild>? guilds, string? currentGuildId, DiscordRestClient? restClient)
    {
        _guilds = guilds;
        _currentGuildId = currentGuildId;
        _restClient = restClient;
        SearchTextBox.Text = string.Empty;
        await ReloadCatalogAsync(null);
    }

    private async Task ReloadCatalogAsync(string? query)
    {
        if (Groups.Count == 0)
        {
            LoadingTextBlock.Visibility = Visibility.Visible;
        }

        var catalog = await StickerService.Instance.BuildPickerCatalogAsync(_guilds, _currentGuildId, _restClient, query);
        Groups.Clear();
        foreach (var group in catalog)
        {
            Groups.Add(group);
        }

        LoadingTextBlock.Visibility = Visibility.Collapsed;
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        await ReloadCatalogAsync(SearchTextBox.Text);
    }

    private void OnStickerItemClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StickerItemViewModel sticker)
        {
            StickerService.Instance.TrackRecentSticker(sticker);
            StickerSelected?.Invoke(sticker);
        }
    }

    private void OnCategoryNavClicked(object sender, RoutedEventArgs e)
    {
        // Category navigation
    }
}
