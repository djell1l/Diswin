using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Windows.Media.Core;
using Windows.Media.Playback;
using DiscordNative.Protocol.Models;

namespace DiscordNative.App.Services;

/// <summary>
/// Service singleton de lecture pour les messages vocaux et pièces jointes audio dans le chat Discord.
/// Permet la lecture, la pause, le défilement (seek), et l'accélération (1x, 1.5x, 2x).
/// </summary>
public sealed class AudioMessagePlayerService : INotifyPropertyChanged
{
    public static AudioMessagePlayerService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private MediaPlayer? _player;
    private readonly DispatcherTimer _timer;
    private DiscordAttachment? _currentAttachment;

    public DiscordAttachment? CurrentAttachment => _currentAttachment;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) > 0.001)
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    private string _timeText = "0:00 / 0:00";
    public string TimeText
    {
        get => _timeText;
        private set
        {
            if (_timeText != value)
            {
                _timeText = value;
                OnPropertyChanged(nameof(TimeText));
            }
        }
    }

    private double _playbackRate = 1.0;
    public double PlaybackRate
    {
        get => _playbackRate;
        private set
        {
            if (Math.Abs(_playbackRate - value) > 0.01)
            {
                _playbackRate = value;
                OnPropertyChanged(nameof(PlaybackRate));
                OnPropertyChanged(nameof(PlaybackRateText));
            }
        }
    }
    public string PlaybackRateText => $"{_playbackRate:0.#}x";

    public AudioMessagePlayerService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
    }

    public void TogglePlay(DiscordAttachment attachment)
    {
        if (_currentAttachment?.Id == attachment.Id && _player != null)
        {
            if (IsPlaying)
            {
                _player.Pause();
                IsPlaying = false;
                _timer.Stop();
            }
            else
            {
                _player.Play();
                IsPlaying = true;
                _timer.Start();
            }
            return;
        }

        Stop();
        _currentAttachment = attachment;
        OnPropertyChanged(nameof(CurrentAttachment));

        try
        {
            _player = new MediaPlayer
            {
                Volume = 1.0,
                Source = MediaSource.CreateFromUri(new Uri(attachment.Url))
            };
            _player.PlaybackSession.PlaybackRate = _playbackRate;
            _player.MediaEnded += (s, e) =>
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    Stop();
                });
            };

            _player.Play();
            IsPlaying = true;
            _timer.Start();
        }
        catch
        {
            Stop();
        }
    }

    public void Seek(double ratio)
    {
        if (_player?.PlaybackSession == null) return;
        var total = _player.PlaybackSession.NaturalDuration;
        if (total.TotalSeconds > 0)
        {
            var target = TimeSpan.FromSeconds(total.TotalSeconds * Math.Clamp(ratio, 0.0, 1.0));
            _player.PlaybackSession.Position = target;
            UpdateProgress();
        }
    }

    public void CyclePlaybackRate()
    {
        PlaybackRate = PlaybackRate switch
        {
            1.0 => 1.5,
            1.5 => 2.0,
            _ => 1.0
        };
        if (_player?.PlaybackSession != null)
        {
            _player.PlaybackSession.PlaybackRate = PlaybackRate;
        }
    }

    public void Stop()
    {
        _timer.Stop();
        if (_player != null)
        {
            try { _player.Pause(); _player.Dispose(); } catch { }
            _player = null;
        }
        _currentAttachment = null;
        IsPlaying = false;
        Progress = 0.0;
        TimeText = "0:00 / 0:00";
        OnPropertyChanged(nameof(CurrentAttachment));
    }

    private void OnTimerTick(object? sender, object e)
    {
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (_player?.PlaybackSession == null) return;
        var pos = _player.PlaybackSession.Position;
        var dur = _player.PlaybackSession.NaturalDuration;

        if (dur.TotalSeconds > 0)
        {
            Progress = Math.Clamp(pos.TotalSeconds / dur.TotalSeconds, 0.0, 1.0);
            TimeText = $"{FormatTime(pos)} / {FormatTime(dur)}";
        }
        else if (_currentAttachment?.DurationSecs is double expectedDur && expectedDur > 0)
        {
            var total = TimeSpan.FromSeconds(expectedDur);
            Progress = Math.Clamp(pos.TotalSeconds / expectedDur, 0.0, 1.0);
            TimeText = $"{FormatTime(pos)} / {FormatTime(total)}";
        }
    }

    private static string FormatTime(TimeSpan ts) =>
        ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");

    public bool IsAttachmentPlaying(DiscordAttachment? attachment) =>
        attachment != null && _currentAttachment?.Id == attachment.Id && IsPlaying;
}
