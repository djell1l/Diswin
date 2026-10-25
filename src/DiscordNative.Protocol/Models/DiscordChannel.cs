using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public enum DiscordChannelType
{
    GuildText = 0,
    Dm = 1,
    GuildVoice = 2,
    GroupDm = 3,
    GuildCategory = 4,
    GuildAnnouncement = 5,
    AnnouncementThread = 10,
    PublicThread = 11,
    PrivateThread = 12,
    GuildStageVoice = 13,
    GuildForum = 15,
}

public sealed class DiscordChannel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public DiscordChannelType Type { get; set; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; set; }

    private string? _name;
    [JsonPropertyName("name")]
    public string? Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(CategoryNameUpper));
            }
        }
    }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("position")]
    public int Position { get; set; }

    [JsonPropertyName("recipients")]
    public List<DiscordUser>? Recipients { get; set; }

    [JsonPropertyName("last_message_id")]
    public string? LastMessageId { get; set; }

    private string? _icon;
    [JsonPropertyName("icon")]
    public string? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
                OnPropertyChanged(nameof(IconUrl));
                OnPropertyChanged(nameof(HasGroupIcon));
            }
        }
    }

    [JsonPropertyName("user_limit")]
    public int? UserLimit { get; set; }

    [JsonPropertyName("permissions")]
    public string? Permissions { get; set; }

    [JsonPropertyName("permission_overwrites")]
    public List<DiscordPermissionOverwrite>? PermissionOverwrites { get; set; }

    [JsonPropertyName("thread_metadata")]
    public DiscordThreadMetadata? ThreadMetadata { get; set; }

    [JsonPropertyName("message_count")]
    public int? MessageCount { get; set; }

    [JsonPropertyName("member_count")]
    public int? MemberCount { get; set; }

    [JsonPropertyName("available_tags")]
    public List<DiscordForumTag>? AvailableTags { get; set; }

    [JsonPropertyName("applied_tags")]
    public List<string>? AppliedTags { get; set; }


    private bool _canView = true;
    [JsonIgnore]
    public bool CanView
    {
        get => _canView;
        set
        {
            if (_canView != value)
            {
                _canView = value;
                OnPropertyChanged(nameof(CanView));
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(ShowVoiceSpeakerIcon));
            }
        }
    }

    private bool _canConnect = true;
    [JsonIgnore]
    public bool CanConnect
    {
        get => _canConnect;
        set
        {
            if (_canConnect != value)
            {
                _canConnect = value;
                OnPropertyChanged(nameof(CanConnect));
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(ShowVoiceSpeakerIcon));
            }
        }
    }

    [JsonIgnore]
    public bool IsLocked => IsVoiceChannel && (!CanConnect || !CanView);

    [JsonIgnore]
    public bool ShowVoiceSpeakerIcon => IsVoiceChannel && !IsLocked;

    public bool IsTextLike => Type is DiscordChannelType.GuildText
        or DiscordChannelType.Dm
        or DiscordChannelType.GroupDm
        or DiscordChannelType.GuildAnnouncement
        or DiscordChannelType.PublicThread
        or DiscordChannelType.PrivateThread
        or DiscordChannelType.AnnouncementThread;

    public bool IsVoiceChannel => Type is DiscordChannelType.GuildVoice or DiscordChannelType.GuildStageVoice;
    public bool IsCategory => Type == DiscordChannelType.GuildCategory;
    public bool IsNotCategory => Type != DiscordChannelType.GuildCategory;
    public bool IsThread => Type is DiscordChannelType.PublicThread or DiscordChannelType.PrivateThread or DiscordChannelType.AnnouncementThread;
    public bool IsForumChannel => Type == DiscordChannelType.GuildForum;
    public bool ShowHashIcon => !IsVoiceChannel && !IsForumChannel;
    public bool HasTags => AppliedTags != null && AppliedTags.Count > 0;
    [JsonIgnore]
    public ObservableCollection<DiscordForumTag> ResolvedTags { get; } = new();
    [JsonIgnore]
    public bool HasResolvedTags => ResolvedTags.Count > 0;
    public string ThreadMessageCountText => MessageCount.HasValue ? $"{MessageCount.Value} message{(MessageCount.Value > 1 ? "s" : "")}" : "Fil actif";
    public bool IsInParentCategory => !IsCategory && !string.IsNullOrEmpty(ParentId);
    public string ChannelRowMargin => IsInParentCategory ? "12,0,0,0" : "0,0,0,0";
    public string CategoryNameUpper => (Name ?? DisplayName).ToUpperInvariant();


    private bool _isCollapsed;
    [JsonIgnore]
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (_isCollapsed != value)
            {
                _isCollapsed = value;
                OnPropertyChanged(nameof(IsCollapsed));
                OnPropertyChanged(nameof(CategoryChevronGlyph));
            }
        }
    }

    [JsonIgnore]
    public string CategoryChevronGlyph => _isCollapsed ? "\uE76C" : "\uE70D";

    private bool _isCategoryVisible = true;
    [JsonIgnore]
    public bool IsCategoryVisible
    {
        get => _isCategoryVisible;
        set
        {
            if (_isCategoryVisible != value)
            {
                _isCategoryVisible = value;
                OnPropertyChanged(nameof(IsCategoryVisible));
            }
        }
    }

    [JsonIgnore]
    public ObservableCollection<DiscordUser> ConnectedVoiceMembers { get; } = new();

    [JsonIgnore]
    public bool HasConnectedVoiceMembers => ConnectedVoiceMembers.Count > 0;

    [JsonIgnore]
    public string VoiceLimitText => UserLimit.HasValue && UserLimit.Value > 0
        ? $"{ConnectedVoiceMembers.Count:D2} / {UserLimit.Value:D2}"
        : $"{ConnectedVoiceMembers.Count}";

    [JsonIgnore]
    public string VoiceSpeakerColorBrush => HasConnectedVoiceMembers ? "#23A55A" : "#949BA4";

    private int _mentionCount;
    [JsonIgnore]
    public int MentionCount
    {
        get => _mentionCount;
        set
        {
            if (_mentionCount != value)
            {
                _mentionCount = Math.Max(0, value);
                OnPropertyChanged(nameof(MentionCount));
                OnPropertyChanged(nameof(HasMentions));
                OnPropertyChanged(nameof(MentionCountText));
            }
        }
    }

    [JsonIgnore]
    public bool HasMentions => _mentionCount > 0;

    [JsonIgnore]
    public string MentionCountText => _mentionCount > 99 ? "99+" : _mentionCount.ToString();

    private bool _hasUnread;
    [JsonIgnore]
    public bool HasUnread
    {
        get => _hasUnread && !_isMuted;
        set
        {
            if (_hasUnread != value)
            {
                _hasUnread = value;
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    private bool _isMuted;
    [JsonIgnore]
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                OnPropertyChanged(nameof(IsMuted));
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(ChannelItemOpacity));
            }
        }
    }

    [JsonIgnore]
    public double ChannelItemOpacity => _isMuted ? 0.5 : 1.0;

    public void NotifyVoiceMembersChanged()
    {
        OnPropertyChanged(nameof(ConnectedVoiceMembers));
        OnPropertyChanged(nameof(HasConnectedVoiceMembers));
        OnPropertyChanged(nameof(VoiceLimitText));
        OnPropertyChanged(nameof(VoiceSpeakerColorBrush));
    }

    /// <summary>Nom d'affichage : pour un DM, on utilise le nom du destinataire.</summary>
    public string DisplayName => Type switch
    {
        DiscordChannelType.Dm => Recipients?.FirstOrDefault()?.DisplayName ?? "DM inconnu",
        DiscordChannelType.GroupDm => Name ?? string.Join(", ", Recipients?.Select(r => r.DisplayName) ?? Array.Empty<string>()),
        _ => Name ?? "salon-inconnu",
    };

    /// <summary>URL de l'icône : photo de profil pour un DM, icône personnalisée pour un groupe DM.</summary>
    public string? IconUrl => Type switch
    {
        DiscordChannelType.Dm => Recipients?.FirstOrDefault()?.AvatarUrl,
        DiscordChannelType.GroupDm => !string.IsNullOrEmpty(Icon)
            ? $"https://cdn.discordapp.com/channel-icons/{Id}/{Icon}.png?size=128"
            : null,
        _ => null
    };

    [JsonIgnore] public bool IsGroupDm => Type == DiscordChannelType.GroupDm;
    [JsonIgnore] public bool HasGroupIcon => IsGroupDm && !string.IsNullOrEmpty(Icon);
    [JsonIgnore] public bool ShowDefaultGroupIcon => IsGroupDm && string.IsNullOrEmpty(Icon);

    [JsonIgnore] public string? GuildName { get; set; }
    [JsonIgnore] public string? GuildIconUrl { get; set; }
    [JsonIgnore] public bool HasGuildIcon => !string.IsNullOrEmpty(GuildIconUrl);
    [JsonIgnore] public string HeaderContextText => !string.IsNullOrEmpty(GuildName) ? $"{GuildName} › #{DisplayName}" : DisplayName;
}

public sealed class DiscordPermissionOverwrite
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; } // 0 = role, 1 = member

    [JsonPropertyName("allow")]
    public string? Allow { get; set; }

    [JsonPropertyName("deny")]
    public string? Deny { get; set; }

    [JsonIgnore]
    public ulong AllowBits => ulong.TryParse(Allow, out var b) ? b : 0UL;

    [JsonIgnore]
    public ulong DenyBits => ulong.TryParse(Deny, out var b) ? b : 0UL;
}
