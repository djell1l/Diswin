using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordSoundboardSound : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    [JsonPropertyName("sound_id")]
    public string SoundId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 1.0;

    [JsonPropertyName("emoji_id")]
    public string? EmojiId { get; set; }

    [JsonPropertyName("emoji_name")]
    public string? EmojiName { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; } = true;

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonIgnore]
    public string AudioUrl => $"https://cdn.discordapp.com/soundboard-sounds/{SoundId}";

    [JsonIgnore]
    public string DisplayEmoji => !string.IsNullOrEmpty(EmojiName) ? EmojiName : "🔊";

    [JsonIgnore]
    public string DisplayName => Name;

    private bool _isPlaying;
    [JsonIgnore]
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }
}

public sealed class DiscordSoundboardGuildResponse
{
    [JsonPropertyName("items")]
    public List<DiscordSoundboardSound>? Items { get; set; }
}

public sealed class DiscordSoundboardEffect
{
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("sound_id")]
    public string SoundId { get; set; } = string.Empty;

    [JsonPropertyName("sound_volume")]
    public double SoundVolume { get; set; } = 1.0;

    [JsonPropertyName("source_guild_id")]
    public string? SourceGuildId { get; set; }
}

public sealed class DiscordSoundboardGroup : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Header { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE74F";
    public ObservableCollection<DiscordSoundboardSound> Sounds { get; } = new();

    public int Count => Sounds.Count;
    public bool HasSounds => Sounds.Count > 0;

    public void NotifyChanged()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasSounds));
    }
}
