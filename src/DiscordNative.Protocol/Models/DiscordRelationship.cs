using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public enum DiscordRelationshipType
{
    None = 0,
    Friend = 1,
    Blocked = 2,
    PendingIncoming = 3,
    PendingOutgoing = 4,
}

public sealed class DiscordRelationship : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;
        if (handler is null) return;
        try
        {
            DiscordNative.Protocol.App.RunOnUiThread(() =>
            {
                try { handler.Invoke(this, new PropertyChangedEventArgs(propertyName)); } catch { }
            });
        }
        catch { }
    }

    public void NotifyPresenceChanged()
    {
        OnPropertyChanged(nameof(User));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsFriend));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsPendingIncoming));
        OnPropertyChanged(nameof(IsPendingOutgoing));
        OnPropertyChanged(nameof(IsBlocked));
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public DiscordRelationshipType Type { get; set; }

    [JsonPropertyName("user")]
    public DiscordUser User { get; set; } = new();

    public bool IsFriend => Type == DiscordRelationshipType.Friend;
    public bool IsPending => Type is DiscordRelationshipType.PendingIncoming or DiscordRelationshipType.PendingOutgoing;
    public bool IsPendingIncoming => Type == DiscordRelationshipType.PendingIncoming;
    public bool IsPendingOutgoing => Type == DiscordRelationshipType.PendingOutgoing;
    public bool IsBlocked => Type == DiscordRelationshipType.Blocked;
    public bool IsOnline => IsFriend && (User.Status is "online" or "idle" or "dnd");

    public string SubtitleText => Type switch
    {
        DiscordRelationshipType.PendingIncoming => "Demande d'ami reçue",
        DiscordRelationshipType.PendingOutgoing => "Demande d'ami envoyée",
        DiscordRelationshipType.Blocked => "Utilisateur bloqué",
        _ => !string.IsNullOrWhiteSpace(User.ActivitySummaryText)
            ? User.ActivitySummaryText
            : User.Status switch
            {
                "online" => "En ligne",
                "idle" => "Inactif",
                "dnd" => "Ne pas déranger",
                _ => User.UserTag
            }
    };

    public string SubtitleDisplay => !string.IsNullOrWhiteSpace(SubtitleText) ? SubtitleText : User.UserTag;
}

