using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DiscordNative.Protocol.Models;
using DiscordNative.App.ViewModels;

namespace DiscordNative.App.Controls;

public sealed partial class DiscordSoundboardPickerControl : UserControl
{
    public event Action<DiscordSoundboardSound, bool>? SoundSelected; // sound, previewOnly

    private MainViewModel? _viewModel;

    public DiscordSoundboardPickerControl()
    {
        this.InitializeComponent();
    }

    public void Initialize(MainViewModel vm)
    {
        _viewModel = vm;
        GroupsItemsControl.ItemsSource = vm.FilteredSoundboardGroups;
        vm.FilteredSoundboardGroups.CollectionChanged += (_, _) =>
        {
            UpdateEmptyState();
        };
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        if (_viewModel == null) return;
        bool hasItems = _viewModel.FilteredSoundboardGroups.Count > 0;
        EmptyStatePanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && _viewModel != null)
        {
            _viewModel.FilterSoundboardSounds(tb.Text);
            UpdateEmptyState();
        }
    }

    private void OnPlaySoundClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DiscordSoundboardSound sound })
        {
            SoundSelected?.Invoke(sound, false);
        }
    }

    private void OnPreviewSoundClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DiscordSoundboardSound sound })
        {
            SoundSelected?.Invoke(sound, true);
        }
    }
}
