using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using DiscordNative.Protocol.Models;
using DiscordNative.App.Services;

namespace DiscordNative.App.Controls;

public sealed partial class DiscordAudioPlayerControl : UserControl
{
    public static readonly DependencyProperty AttachmentProperty =
        DependencyProperty.Register(nameof(Attachment), typeof(DiscordAttachment), typeof(DiscordAudioPlayerControl),
            new PropertyMetadata(null, OnAttachmentChangedStatic));

    public DiscordAttachment? Attachment
    {
        get => (DiscordAttachment?)GetValue(AttachmentProperty);
        set => SetValue(AttachmentProperty, value);
    }

    private static void OnAttachmentChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DiscordAudioPlayerControl control)
        {
            control.UpdateAttachment(e.NewValue as DiscordAttachment);
        }
    }

    private bool _isUserDraggingSlider;

    public DiscordAudioPlayerControl()
    {
        this.InitializeComponent();
        ProgressSlider.PointerCaptureLost += (_, _) => _isUserDraggingSlider = false;
        ProgressSlider.PointerPressed += (_, _) => _isUserDraggingSlider = true;
        ProgressSlider.PointerReleased += (_, _) => _isUserDraggingSlider = false;
        AudioMessagePlayerService.Instance.PropertyChanged += OnPlayerServicePropertyChanged;
    }


    private void UpdateAttachment(DiscordAttachment? att)
    {
        if (att == null) return;
        TitleTextBlock.Text = att.TitleText;
        TimeTextBlock.Text = att.DurationFormattedText;
        UpdateVisualState();
    }

    private void OnPlayerServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var player = AudioMessagePlayerService.Instance;
        bool isThisPlaying = Attachment != null && player.CurrentAttachment?.Id == Attachment.Id;

        if (e.PropertyName == nameof(player.IsPlaying) || e.PropertyName == nameof(player.CurrentAttachment))
        {
            UpdateVisualState();
        }
        else if (isThisPlaying && e.PropertyName == nameof(player.Progress))
        {
            if (!_isUserDraggingSlider)
            {
                ProgressSlider.Value = player.Progress;
            }
        }
        else if (isThisPlaying && e.PropertyName == nameof(player.TimeText))
        {
            TimeTextBlock.Text = player.TimeText;
        }
        else if (e.PropertyName == nameof(player.PlaybackRateText))
        {
            SpeedButton.Content = player.PlaybackRateText;
        }
    }

    private void UpdateVisualState()
    {
        var player = AudioMessagePlayerService.Instance;
        bool isThisPlaying = Attachment != null && player.CurrentAttachment?.Id == Attachment.Id && player.IsPlaying;
        PlayPauseIcon.Glyph = isThisPlaying ? "\uE769" : "\uE768"; // Pause vs Play

        if (!isThisPlaying && (player.CurrentAttachment?.Id != Attachment?.Id))
        {
            ProgressSlider.Value = 0.0;
            if (Attachment != null)
            {
                TimeTextBlock.Text = Attachment.DurationFormattedText;
            }
        }
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (Attachment == null) return;
        AudioMessagePlayerService.Instance.TogglePlay(Attachment);
        UpdateVisualState();
    }

    private void OnProgressSliderValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        var player = AudioMessagePlayerService.Instance;
        if (Attachment != null && player.CurrentAttachment?.Id == Attachment.Id && player.IsPlaying)
        {
            player.Seek(e.NewValue);
        }
    }

    private void OnSpeedClicked(object sender, RoutedEventArgs e)
    {
        AudioMessagePlayerService.Instance.CyclePlaybackRate();
    }

    private async void OnOpenLinkClicked(object sender, RoutedEventArgs e)
    {
        if (Attachment != null && !string.IsNullOrEmpty(Attachment.Url) && Uri.TryCreate(Attachment.Url, UriKind.Absolute, out var uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }
}
