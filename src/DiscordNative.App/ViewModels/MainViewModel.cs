using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using DiscordNative.Protocol;
using DiscordNative.Protocol.Models;
using DiscordNative.Protocol.Utils;
using DiscordNative.App.Services;
namespace DiscordNative.App.ViewModels;
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DiscordClient _client;
    public DiscordClient Client => _client;
    private DiscordGuild? _selectedGuild;
    public DiscordGuild? SelectedGuild => _selectedGuild;
    private DiscordChannel? _selectedChannel;
    public DiscordChannel? SelectedChannel => _selectedChannel;
    public void ClearActiveChannel()
    {
        CloseSplitViewThread();
        ChannelActiveThreads.Clear();
        IsForumChannelSelected = false;
        IsCreatingForumPost = false;
        ForumPosts.Clear();
        FilteredForumPosts.Clear();
        ForumAvailableTags.Clear();
        _selectedChannel = null;
        SelectedChannelName = "Sélectionnez un salon";
        SelectedChannelTopic = string.Empty;
        Messages.Clear();
        ChannelMembers.Clear();
        OnPropertyChanged(nameof(SelectedChannel));
        OnPropertyChanged(nameof(ShowingFriendsMainStage));
        OnPropertyChanged(nameof(FriendsMainStageVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
    }
    private bool _showingFriends;
    private string _friendsFilter = "All";
    public ObservableCollection<DiscordGuild> Guilds => _client.Guilds;
    public ObservableCollection<DiscordGuild> DisplayGuilds { get; } = new();
    public ObservableCollection<DiscordChannel> Channels { get; } = new();
    public ObservableCollection<DiscordRelationship> Friends => _client.Friends;
    public ObservableCollection<DiscordRelationship> FilteredFriends { get; } = new();
    public ObservableCollection<DiscordChannel> PrivateChannels => _client.PrivateChannels;
    public ObservableCollection<DiscordMessage> Messages { get; } = new();
    public ObservableCollection<DiscordMessage> PinnedMessages { get; } = new();
    public bool HasPinnedMessages => PinnedMessages.Count > 0;
    public string PinnedMessagesCountText => $"Messages épinglés ({PinnedMessages.Count})";
    public ObservableCollection<DiscordMessage> ChannelSearchResults { get; } = new();
    [ObservableProperty]
    private bool isSearchingChannel;
    public bool HasChannelSearchResults => ChannelSearchResults.Count > 0;
    public string ChannelSearchResultsCountText => $"{ChannelSearchResults.Count} résultat{(ChannelSearchResults.Count > 1 ? "s" : "")}";
    [ObservableProperty]
    private bool isSplitThreadOpen;
    partial void OnIsSplitThreadOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(SplitThreadPanelVisibility));
    }
    [ObservableProperty]
    private DiscordChannel? activeSplitThread;
    partial void OnActiveSplitThreadChanged(DiscordChannel? value)
    {
        OnPropertyChanged(nameof(SplitThreadPanelVisibility));
    }
    public ObservableCollection<DiscordMessage> ActiveThreadMessages { get; } = new();
    public ObservableCollection<DiscordChannel> ChannelActiveThreads { get; } = new();
    public bool HasChannelActiveThreads => ChannelActiveThreads.Count > 0;
    public string ChannelActiveThreadsCountText => $"{ChannelActiveThreads.Count} fil{(ChannelActiveThreads.Count > 1 ? "s" : "")}";
    public Microsoft.UI.Xaml.Visibility SplitThreadPanelVisibility => IsSplitThreadOpen && ActiveSplitThread != null
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
    [ObservableProperty]
    private bool isForumChannelSelected;
    partial void OnIsForumChannelSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ForumViewVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
    }
    [ObservableProperty]
    private bool isCreatingForumPost;
    [ObservableProperty]
    private string forumSearchQuery = string.Empty;
    partial void OnForumSearchQueryChanged(string value)
    {
        ApplyForumFilter();
    }
    [ObservableProperty]
    private DiscordForumTag? selectedForumFilterTag;
    partial void OnSelectedForumFilterTagChanged(DiscordForumTag? value)
    {
        ApplyForumFilter();
    }
    public ObservableCollection<DiscordChannel> ForumPosts { get; } = new();
    public ObservableCollection<DiscordChannel> FilteredForumPosts { get; } = new();
    public ObservableCollection<DiscordForumTag> ForumAvailableTags { get; } = new();
    public bool HasFilteredForumPosts => FilteredForumPosts.Count > 0;
    public string ForumPostsCountText => $"{FilteredForumPosts.Count} post{(FilteredForumPosts.Count > 1 ? "s" : "")}";
    public Microsoft.UI.Xaml.Visibility ForumViewVisibility => IsForumChannelSelected && !IsVoiceChannelSelected
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
    [ObservableProperty]
    private bool isDiscoveryOpen;
    partial void OnIsDiscoveryOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(DiscoveryViewVisibility));
        OnPropertyChanged(nameof(DiscoveryPillVisibility));
        OnPropertyChanged(nameof(DiscoveryCompassBackground));
        OnPropertyChanged(nameof(DiscoveryCompassForeground));
    }
    public Microsoft.UI.Xaml.Visibility DiscoveryViewVisibility => IsDiscoveryOpen
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility DiscoveryPillVisibility => IsDiscoveryOpen
        ? Microsoft.UI.Xaml.Visibility.Visible
        : Microsoft.UI.Xaml.Visibility.Collapsed;
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DiscoveryActiveCompassBrush = new(Windows.UI.Color.FromArgb(255, 35, 165, 90)); // #23A55A
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush DiscoveryInactiveCompassBrush = new(Windows.UI.Color.FromArgb(255, 43, 45, 49)); // #2B2D31
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush WhiteBrush = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush ActivePillBrush = new(Windows.UI.Color.FromArgb(255, 88, 101, 242)); // #5865F2 Blurple
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush InactivePillBrush = new(Windows.UI.Color.FromArgb(255, 43, 45, 49)); // #2B2D31
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush ActivePillTextBrush = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush InactivePillTextBrush = new(Windows.UI.Color.FromArgb(255, 219, 222, 225)); // #DBDEE1
    public Microsoft.UI.Xaml.Media.Brush DiscoveryCompassBackground => IsDiscoveryOpen ? DiscoveryActiveCompassBrush : DiscoveryInactiveCompassBrush;
    public Microsoft.UI.Xaml.Media.Brush DiscoveryCompassForeground => IsDiscoveryOpen ? WhiteBrush : DiscoveryActiveCompassBrush;
    public ObservableCollection<DiscordDiscoverableGuild> DiscoverableGuilds { get; } = new();
    public ObservableCollection<DiscordDiscoverableGuild> FilteredDiscoverableGuilds { get; } = new();
    [ObservableProperty]
    private string discoverySearchQuery = string.Empty;
    partial void OnDiscoverySearchQueryChanged(string value)
    {
        ApplyDiscoveryFilter();
    }
    [ObservableProperty]
    private string selectedDiscoveryCategory = "Tous";
    partial void OnSelectedDiscoveryCategoryChanged(string value)
    {
        ApplyDiscoveryFilter();
        OnPropertyChanged(nameof(CategoryTousBrush));
        OnPropertyChanged(nameof(CategoryGamingBrush));
        OnPropertyChanged(nameof(CategoryMusiqueBrush));
        OnPropertyChanged(nameof(CategoryTechBrush));
        OnPropertyChanged(nameof(CategoryEducationBrush));
        OnPropertyChanged(nameof(CategoryTousTextBrush));
        OnPropertyChanged(nameof(CategoryGamingTextBrush));
        OnPropertyChanged(nameof(CategoryMusiqueTextBrush));
        OnPropertyChanged(nameof(CategoryTechTextBrush));
        OnPropertyChanged(nameof(CategoryEducationTextBrush));
    }
    public Microsoft.UI.Xaml.Media.Brush CategoryTousBrush => SelectedDiscoveryCategory == "Tous" ? ActivePillBrush : InactivePillBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryGamingBrush => SelectedDiscoveryCategory == "Gaming" ? ActivePillBrush : InactivePillBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryMusiqueBrush => SelectedDiscoveryCategory == "Musique" ? ActivePillBrush : InactivePillBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryTechBrush => SelectedDiscoveryCategory == "Tech" ? ActivePillBrush : InactivePillBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryEducationBrush => SelectedDiscoveryCategory == "Éducation" ? ActivePillBrush : InactivePillBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryTousTextBrush => SelectedDiscoveryCategory == "Tous" ? ActivePillTextBrush : InactivePillTextBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryGamingTextBrush => SelectedDiscoveryCategory == "Gaming" ? ActivePillTextBrush : InactivePillTextBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryMusiqueTextBrush => SelectedDiscoveryCategory == "Musique" ? ActivePillTextBrush : InactivePillTextBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryTechTextBrush => SelectedDiscoveryCategory == "Tech" ? ActivePillTextBrush : InactivePillTextBrush;
    public Microsoft.UI.Xaml.Media.Brush CategoryEducationTextBrush => SelectedDiscoveryCategory == "Éducation" ? ActivePillTextBrush : InactivePillTextBrush;
    [ObservableProperty]
    private bool isLoadingDiscovery;
    public string DiscoveryResultsCountText => $"{FilteredDiscoverableGuilds.Count} communauté{(FilteredDiscoverableGuilds.Count > 1 ? "s" : "")} trouvée{(FilteredDiscoverableGuilds.Count > 1 ? "s" : "")}";
    public bool HasFilteredDiscoverableGuilds => FilteredDiscoverableGuilds.Count > 0;
    public ObservableCollection<DiscordMessage> RecentMentions { get; } = new();
    public ObservableCollection<DiscordChannel> UnreadChannels { get; } = new();
    public bool HasUnreadChannels => UnreadChannels.Count > 0;
    public string UnreadChannelsCountText => UnreadChannels.Count.ToString();
    public ObservableCollection<DiscordMessage> Bookmarks { get; } = new();
    public bool HasBookmarks => Bookmarks.Count > 0;
    public string BookmarksCountText => Bookmarks.Count.ToString();
    private int _selectedInboxTabIndex = 0; // 0 = Non lus, 1 = Mentions, 2 = Favoris
    public int SelectedInboxTabIndex
    {
        get => _selectedInboxTabIndex;
        set
        {
            if (_selectedInboxTabIndex != value)
            {
                _selectedInboxTabIndex = value;
                OnPropertyChanged(nameof(SelectedInboxTabIndex));
                OnPropertyChanged(nameof(IsInboxUnreadsSelected));
                OnPropertyChanged(nameof(IsInboxMentionsSelected));
                OnPropertyChanged(nameof(IsInboxBookmarksSelected));
                OnPropertyChanged(nameof(InboxUnreadsVisibility));
                OnPropertyChanged(nameof(InboxMentionsVisibility));
                OnPropertyChanged(nameof(InboxBookmarksVisibility));
                OnPropertyChanged(nameof(InboxTabUnreadsBackground));
                OnPropertyChanged(nameof(InboxTabMentionsBackground));
                OnPropertyChanged(nameof(InboxTabBookmarksBackground));
            }
        }
    }
    public bool IsInboxUnreadsSelected => SelectedInboxTabIndex == 0;
    public bool IsInboxMentionsSelected => SelectedInboxTabIndex == 1;
    public bool IsInboxBookmarksSelected => SelectedInboxTabIndex == 2;
    public string InboxTabUnreadsBackground => IsInboxUnreadsSelected ? "#2B2D31" : "Transparent";
    public string InboxTabMentionsBackground => IsInboxMentionsSelected ? "#2B2D31" : "Transparent";
    public string InboxTabBookmarksBackground => IsInboxBookmarksSelected ? "#2B2D31" : "Transparent";
    public bool HasMentionsInInbox => RecentMentions.Count > 0;
    public bool HasNoMentionsInInbox => RecentMentions.Count == 0;
    public Microsoft.UI.Xaml.Visibility InboxUnreadsVisibility => IsInboxUnreadsSelected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility InboxMentionsVisibility => IsInboxMentionsSelected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility InboxBookmarksVisibility => IsInboxBookmarksSelected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public ObservableCollection<DiscordUser> ChannelMembers { get; } = new();
    public ObservableCollection<MemberActivityCardViewModel> RecentMemberActivities { get; } = new();
    public bool HasRecentMemberActivities => RecentMemberActivities.Count > 0;
    public ObservableCollection<DiscordUser> OnlineMembers { get; } = new();
    public ObservableCollection<DiscordUser> OfflineMembers { get; } = new();
    public int OnlineMembersCount => OnlineMembers.Count;
    public int OfflineMembersCount => OfflineMembers.Count;
    public bool HasOfflineMembers => OfflineMembers.Count > 0;
    public ObservableCollection<DiscordMemberGroup> MemberGroups { get; } = new();
    public bool HasMemberGroups => MemberGroups.Count > 0;
    public ObservableCollection<DiscordSoundboardGroup> SoundboardGroups { get; } = new();
    public ObservableCollection<DiscordSoundboardGroup> FilteredSoundboardGroups { get; } = new();
    public bool HasSoundboardGroups => FilteredSoundboardGroups.Count > 0;
    public ObservableCollection<AutocompleteItemViewModel> AutocompleteSuggestions { get; } = new();
    [ObservableProperty]
    private bool isAutocompleteOpen;
    [ObservableProperty]
    private string autocompleteCategoryTitle = "MEMBRES DU SERVEUR";
    [ObservableProperty]
    private int autocompleteSelectedIndex = -1;
    public ObservableCollection<DiscordGifItem> GifResults { get; } = new();

    // IntÃ©gration Ã©cosystÃ¨me Jay â€¢ SoundWin
    public Services.SoundWinBridgeService SoundWinBridge { get; } = new();
    public string SoundWinPlayIcon => SoundWinBridge.IsPlaying ? "\uE769" : "\uE768";
    public string SoundWinPlayTooltip => SoundWinBridge.IsPlaying ? "Mettre en pause" : "Reprendre la lecture";
    [ObservableProperty]
    private bool isLoadingGifs;
    public bool HasSelectedGuild => _selectedGuild != null;
    public bool HasSelectedGuildBanner => _selectedGuild?.HasBanner == true;
    public string? SelectedGuildBannerUrl => _selectedGuild?.BannerUrl;
    public string SelectedGuildBoostBadge => _selectedGuild?.BoostBadgeText ?? string.Empty;
    public bool HasSelectedGuildBoosts => _selectedGuild?.HasBoosts == true;
    [ObservableProperty]
    private string selectedGuildName = "Sélectionnez un serveur";
    [ObservableProperty]
    private string selectedChannelName = "Sélectionnez un salon";
    [ObservableProperty]
    private string selectedChannelTopic = string.Empty;
    [ObservableProperty]
    private string currentUserName = "...";
    [ObservableProperty]
    private bool isVoiceConnected;
    [ObservableProperty]
    private string connectedVoiceChannelName = string.Empty;
    [ObservableProperty]
    private bool isVoiceChannelSelected;
    [ObservableProperty]
    private bool isMuted;
    [ObservableProperty]
    private bool isDeafened;
    [ObservableProperty]
    private bool isKrispEnabled = true;
    [ObservableProperty]
    private bool isScreenSharing;
    [ObservableProperty]
    private bool isCameraEnabled;
    [ObservableProperty]
    private string screenShareTitle = string.Empty;
    [ObservableProperty]
    private string screenShareQualityBadge = "1080p • 60 FPS";
    [ObservableProperty]
    private string currentStatus = "online";
    [ObservableProperty]
    private string searchFilterText = string.Empty;
    [ObservableProperty]
    private bool isMemberListOpen = true;
    private DiscordMessage? _replyingToMessage;
    public DiscordMessage? ReplyingToMessage
    {
        get => _replyingToMessage;
        set
        {
            if (SetProperty(ref _replyingToMessage, value))
            {
                if (value is not null) _editingMessage = null;
                NotifyComposerBannerChanged();
            }
        }
    }
    private DiscordMessage? _editingMessage;
    public DiscordMessage? EditingMessage
    {
        get => _editingMessage;
        set
        {
            if (SetProperty(ref _editingMessage, value))
            {
                if (value is not null) _replyingToMessage = null;
                NotifyComposerBannerChanged();
            }
        }
    }
    public bool HasComposerBanner => ReplyingToMessage is not null || EditingMessage is not null;
    public Microsoft.UI.Xaml.Visibility ComposerBannerVisibility => HasComposerBanner ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string ComposerBannerText => EditingMessage is not null
        ? $"Modification du message : {EditingMessage.Content}"
        : ReplyingToMessage is not null
            ? $"Réponse à @{ReplyingToMessage.Author.DisplayName} : {ReplyingToMessage.ReplyPreview}"
            : string.Empty;
    private void NotifyComposerBannerChanged()
    {
        OnPropertyChanged(nameof(HasComposerBanner));
        OnPropertyChanged(nameof(ComposerBannerVisibility));
        OnPropertyChanged(nameof(ComposerBannerText));
    }
    public void CancelComposerAction()
    {
        ReplyingToMessage = null;
        EditingMessage = null;
    }
    public ObservableCollection<DiscordChannel> FilteredChannels { get; } = new();
    public ObservableCollection<DiscordUser> VoiceParticipants { get; } = new();
    public double VoiceTileWidth => VoiceParticipants.Count <= 1 ? 640 : (VoiceParticipants.Count <= 4 ? 420 : 280);
    public double VoiceTileHeight => VoiceParticipants.Count <= 1 ? 400 : (VoiceParticipants.Count <= 4 ? 260 : 190);
    public double VoiceTileAvatarSize => VoiceParticipants.Count <= 1 ? 128 : (VoiceParticipants.Count <= 4 ? 96 : 72);
    public double VoiceTileSpeakingRingSize => VoiceParticipants.Count <= 1 ? 140 : (VoiceParticipants.Count <= 4 ? 106 : 80);
    public void NotifyVoiceLayoutChanged()
    {
        OnPropertyChanged(nameof(VoiceTileWidth));
        OnPropertyChanged(nameof(VoiceTileHeight));
        OnPropertyChanged(nameof(VoiceTileAvatarSize));
        OnPropertyChanged(nameof(VoiceTileSpeakingRingSize));
    }
    public Microsoft.UI.Xaml.Media.SolidColorBrush StatusDotBrush => new(CurrentStatus switch
    {
        "online" => Windows.UI.Color.FromArgb(255, 0x23, 0xA5, 0x5A),
        "idle" => Windows.UI.Color.FromArgb(255, 0xF0, 0xB2, 0x32),
        "dnd" => Windows.UI.Color.FromArgb(255, 0xF2, 0x3F, 0x43),
        _ => Windows.UI.Color.FromArgb(255, 0x80, 0x84, 0x8E)
    });
    public string CurrentStatusDisplayName => CurrentStatus switch
    {
        "online" => "En ligne",
        "idle" => "Inactif",
        "dnd" => "Ne pas déranger",
        "invisible" => "Invisible",
        _ => CurrentStatus
    };
    public string CurrentUserSubStatusText => !string.IsNullOrWhiteSpace(_client.CurrentUser?.ActivitySummaryText)
        ? _client.CurrentUser.ActivitySummaryText
        : CurrentStatusDisplayName;
    public string CurrentUserCustomStatusText => _client.CurrentUser?.CustomStatusText ?? string.Empty;
    public string CurrentUserCustomStatusEmoji => _client.CurrentUser?.CustomStatusEmoji ?? string.Empty;
    public bool HasCustomStatus => !string.IsNullOrWhiteSpace(CurrentUserCustomStatusText) || !string.IsNullOrWhiteSpace(CurrentUserCustomStatusEmoji);
    public string CustomStatusButtonText => HasCustomStatus
        ? (!string.IsNullOrWhiteSpace(CurrentUserCustomStatusEmoji) ? $"{CurrentUserCustomStatusEmoji} {CurrentUserCustomStatusText}".Trim() : CurrentUserCustomStatusText)
        : "Définir un statut personnalisé";
    public string CurrentUserCustomStatusSubtitle => HasCustomStatus ? "Modifier le statut" : string.Empty;
    public bool CurrentUserIsInVoice => _client.CurrentUser?.IsInVoice ?? false;
    public DiscordActivity? CurrentUserRichPresence => _client.CurrentUser?.PrimaryActivity;
    public Microsoft.UI.Xaml.Visibility CurrentUserRichPresenceVisibility => CurrentUserRichPresence is not null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string CurrentUserRpcHeader => CurrentUserRichPresence?.HeaderLabel ?? "EN TRAIN DE JOUER";
    public string CurrentUserRpcName => CurrentUserRichPresence?.Name ?? string.Empty;
    public string CurrentUserRpcDetails => CurrentUserRichPresence?.Details ?? string.Empty;
    public Microsoft.UI.Xaml.Visibility CurrentUserRpcDetailsVisibility => (CurrentUserRichPresence?.HasDetails == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string CurrentUserRpcState => CurrentUserRichPresence?.State ?? string.Empty;
    public Microsoft.UI.Xaml.Visibility CurrentUserRpcStateVisibility => (CurrentUserRichPresence?.HasState == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string CurrentUserRpcElapsed => CurrentUserRichPresence?.ElapsedText ?? string.Empty;
    public Microsoft.UI.Xaml.Visibility CurrentUserRpcElapsedVisibility => (CurrentUserRichPresence?.HasElapsed == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string? CurrentUserRpcLargeImageUrl => CurrentUserRichPresence?.LargeImageUrl;
    public Microsoft.UI.Xaml.Visibility CurrentUserRpcLargeImageVisibility => (CurrentUserRichPresence?.HasLargeImage == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public string? CurrentUserRpcSmallImageUrl => CurrentUserRichPresence?.SmallImageUrl;
    public Microsoft.UI.Xaml.Visibility CurrentUserRpcSmallImageVisibility => (CurrentUserRichPresence?.HasSmallImage == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public double MuteOpacity => IsMuted ? 0.4 : 1.0;
    public double DeafenOpacity => IsDeafened ? 0.4 : 1.0;
    public double KrispOpacity => IsKrispEnabled ? 1.0 : 0.4;
    public Microsoft.UI.Xaml.Media.SolidColorBrush KrispIconBrush => new(IsKrispEnabled
        ? Windows.UI.Color.FromArgb(255, 0x23, 0xA5, 0x5A)
        : Windows.UI.Color.FromArgb(255, 0x94, 0x9B, 0xA4));
    public string KrispTooltipText => IsKrispEnabled
        ? "Suppression de bruit Krisp : Activée (100% Gratuit • Cliquez pour désactiver)"
        : "Suppression de bruit Krisp : Désactivée (Cliquez pour activer)";
    public double CurrentUserSpeakingRingOpacity => (_client.CurrentUser?.IsSpeaking == true) ? 1.0 : 0.0;
    public Microsoft.UI.Xaml.Visibility ScreenShareStageVisibility => IsScreenSharing ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public double ScreenShareOpacity => IsScreenSharing ? 1.0 : 0.75;
    public Microsoft.UI.Xaml.Media.SolidColorBrush ScreenShareIconBrush => new(IsScreenSharing
        ? Windows.UI.Color.FromArgb(255, 0x23, 0xA5, 0x5A)
        : Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
    public string ScreenShareTooltipText => IsScreenSharing
        ? $"Partage d'écran en cours ({ScreenShareTitle}) — Cliquez pour modifier ou arrêter"
        : "Partager votre écran (Go Live 720p/1080p 60 FPS + Audio système)";
    public double CameraOpacity => IsCameraEnabled ? 1.0 : 0.75;
    public Microsoft.UI.Xaml.Media.SolidColorBrush CameraIconBrush => new(IsCameraEnabled
        ? Windows.UI.Color.FromArgb(255, 0x23, 0xA5, 0x5A)
        : Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF));
    public string CameraTooltipText => IsCameraEnabled
        ? "Caméra activée — Cliquez pour désactiver"
        : "Activer la caméra vidéo";
    public bool ShowingFriendsMainStage => _showingFriends && _selectedChannel == null && !IsDiscoveryOpen;
    public Microsoft.UI.Xaml.Visibility FriendsMainStageVisibility => ShowingFriendsMainStage ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility VoiceViewVisibility => IsVoiceChannelSelected ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility TextViewVisibility => IsVoiceChannelSelected || IsForumChannelSelected || ShowingFriendsMainStage ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public Microsoft.UI.Xaml.Visibility MemberListVisibility => IsMemberListOpen && !IsVoiceChannelSelected && !IsForumChannelSelected && !ShowingFriendsMainStage ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility TopicVisibility => string.IsNullOrWhiteSpace(SelectedChannelTopic) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public DiscordUser? CurrentUser => _client.CurrentUser;
    public string? CurrentUserAvatarUrl => _client.CurrentUser?.AvatarUrl;
    public string? CurrentUserBannerUrl => _client.CurrentUser?.BannerUrl;
    public bool CurrentUserHasBanner => _client.CurrentUser?.HasBanner ?? false;
    public string CurrentUserTag => _client.CurrentUser?.UserTag ?? "@user";
    public string CurrentUserBio => string.IsNullOrWhiteSpace(_client.CurrentUser?.Bio) ? "Aucune bio définie." : _client.CurrentUser.Bio;
    public string CurrentUserPronouns => _client.CurrentUser?.Pronouns ?? string.Empty;
    public Microsoft.UI.Xaml.Visibility CurrentUserPronounsVisibility => string.IsNullOrWhiteSpace(_client.CurrentUser?.Pronouns) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public string CurrentUserClanTag => _client.CurrentUser?.ClanTag ?? string.Empty;
    public string? CurrentUserClanBadgeUrl => _client.CurrentUser?.ClanBadgeUrl;
    public Microsoft.UI.Xaml.Visibility CurrentUserClanVisibility => (_client.CurrentUser?.HasClan == true) ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public ObservableCollection<DiscordBadge> CurrentUserBadges { get; } = new();
    public Microsoft.UI.Xaml.Visibility CurrentUserBadgesVisibility => CurrentUserBadges.Count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public ObservableCollection<DiscordWidgetGame> CurrentUserGameWidgets { get; } = new();
    public Microsoft.UI.Xaml.Visibility CurrentUserGameWidgetsVisibility => CurrentUserGameWidgets.Count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public ObservableCollection<DiscordConnectedAccount> CurrentUserConnectedAccounts { get; } = new();
    public Microsoft.UI.Xaml.Visibility CurrentUserConnectedAccountsVisibility => CurrentUserConnectedAccounts.Count > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public DiscordWidgetGame? CurrentUserFavoriteGame => CurrentUserGameWidgets.FirstOrDefault(g => g.IsFavorite);
    public Microsoft.UI.Xaml.Visibility CurrentUserFavoriteGameVisibility => CurrentUserFavoriteGame is not null ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public void RefreshCurrentUserGameWidgets()
    {
        OnPropertyChanged(nameof(CurrentUserFavoriteGame));
        OnPropertyChanged(nameof(CurrentUserFavoriteGameVisibility));
        OnPropertyChanged(nameof(CurrentUserGameWidgetsVisibility));
    }
    public Microsoft.UI.Xaml.Visibility ShowingGuildChannels => _showingFriends ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    public Microsoft.UI.Xaml.Visibility ShowingFriendsView => _showingFriends ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public int TotalDmMentionCount => _client.PrivateChannels.Sum(c => c.MentionCount);
    public bool HasDmMentions => TotalDmMentionCount > 0;
    public string DmMentionCountText => TotalDmMentionCount > 99 ? "99+" : TotalDmMentionCount.ToString();
    public void NotifyDmMentionsChanged()
    {
        OnPropertyChanged(nameof(TotalDmMentionCount));
        OnPropertyChanged(nameof(HasDmMentions));
        OnPropertyChanged(nameof(DmMentionCountText));
    }
    private readonly Microsoft.UI.Xaml.DispatcherTimer _rpcTickerTimer = new();
    private readonly Microsoft.UI.Xaml.DispatcherTimer _presenceDebounceTimer = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _pendingPresenceUserIds = new();

    public MainViewModel(DiscordClient client)
    {
        _client = client;
        IsKrispEnabled = App.Settings.Current.EnableKrispNoiseSuppression;
        _client.IsKrispEnabled = IsKrispEnabled;
        _client.MessageReceived += OnMessageReceived;
        _client.MessageUpdated += OnMessageUpdated;
        _client.MessageDeleted += OnMessageDeleted;
        _client.ReactionAdded += OnReactionAdded;
        _client.VoiceParticipantsChanged += OnVoiceParticipantsChanged;
        _client.VoiceSpeakingChanged += OnVoiceSpeakingChanged;
        _client.PresenceUpdated += OnPresenceUpdated;
        _client.GuildMembersUpdated += OnGuildMembersUpdated;
        _client.VoiceChannelEffectReceived += OnVoiceChannelEffectReceived;
        _client.Friends.CollectionChanged += (_, _) => RefreshFilteredFriends();
        _client.GuildFoldersUpdated += RefreshDisplayGuilds;
        _client.Guilds.CollectionChanged += (_, _) => RefreshDisplayGuilds();
        RefreshDisplayGuilds();
        _rpcTickerTimer.Interval = TimeSpan.FromSeconds(1);
        _rpcTickerTimer.Tick += (_, _) =>
        {
            if (CurrentUserRichPresence?.HasElapsed == true)
            {
                CurrentUserRichPresence.NotifyTimeChanged();
                OnPropertyChanged(nameof(CurrentUserRpcElapsed));
                CurrentUser?.NotifyActivityTimesChanged();
            }
        };
        _rpcTickerTimer.Start();
        _presenceDebounceTimer.Interval = TimeSpan.FromMilliseconds(450);
        _presenceDebounceTimer.Tick += (_, _) =>
        {
            _presenceDebounceTimer.Stop();
            FlushPendingPresences();
        };
        SoundWinBridge.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SoundWinBridge.IsPlaying))
            {
                OnPropertyChanged(nameof(SoundWinPlayIcon));
                OnPropertyChanged(nameof(SoundWinPlayTooltip));
            }
        };
        LoadBookmarks();
    }
    private void OnVoiceSpeakingChanged(string userId, bool isSpeaking)
    {
        if (userId == _client.CurrentUser?.Id)
        {
            OnPropertyChanged(nameof(CurrentUserSpeakingRingOpacity));
        }
        foreach (var vp in VoiceParticipants.Where(u => u.Id == userId))
        {
            vp.IsSpeaking = isSpeaking;
        }
    }
    private void OnPresenceUpdated(string userId)
    {
        if (userId == _client.CurrentUser?.Id)
        {
            NotifyCurrentUserRichPresenceChanged();
        }
        _pendingPresenceUserIds[userId] = 1;
        Protocol.App.RunOnUiThread(() =>
        {
            _presenceDebounceTimer.Stop();
            _presenceDebounceTimer.Start();
        });
    }

    private void FlushPendingPresences()
    {
        if (_pendingPresenceUserIds.IsEmpty) return;
        var userIds = _pendingPresenceUserIds.Keys.ToList();
        _pendingPresenceUserIds.Clear();

        var idSet = new HashSet<string>(userIds, StringComparer.OrdinalIgnoreCase);
        foreach (var member in ChannelMembers)
        {
            if (member != null && idSet.Contains(member.Id))
            {
                _client.ApplyCachedPresenceToUser(member);
            }
        }
        SyncOnlineOfflineMembers();
        RefreshRecentMemberActivities();
    }
    private void OnGuildMembersUpdated(string guildId)
    {
        if (_selectedGuild?.Id == guildId)
        {
            var cached = _client.GetCachedGuildMembers(guildId);
            if (cached.Count > 0)
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    var seen = new HashSet<string>(ChannelMembers.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
                    foreach (var m in cached)
                    {
                        if (m != null && !string.IsNullOrEmpty(m.Id) && seen.Add(m.Id))
                        {
                            ChannelMembers.Add(m);
                        }
                    }
                    SyncOnlineOfflineMembers();
                    RefreshRecentMemberActivities();
                });
            }
        }
    }
    public void SyncOnlineOfflineMembers()
    {
        Protocol.App.RunOnUiThread(() =>
        {
            // Déduplication absolue et préservation de la dernière référence
            var memberDict = new Dictionary<string, DiscordUser>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in ChannelMembers)
            {
                if (m != null && !string.IsNullOrEmpty(m.Id))
                    memberDict[m.Id] = m;
            }
            if (_selectedGuild != null)
            {
                var cached = _client.GetCachedGuildMembers(_selectedGuild.Id);
                foreach (var m in cached)
                {
                    if (m != null && !string.IsNullOrEmpty(m.Id))
                        memberDict[m.Id] = m;
                }
            }
            var allMembers = memberDict.Values.ToList();
            foreach (var m in allMembers)
            {
                _client.ApplyCachedPresenceToUser(m);
                var (_, vcName) = _client.GetUserVoiceChannel(m.Id);
                m.CurrentVoiceChannelName = vcName;
                if (_selectedGuild != null)
                {
                    m.UpdateDisplayRoles(_selectedGuild.Roles);
                }
            }
            var online = allMembers.Where(m => m.Status != "offline").OrderBy(m => m.DisplayName).ToList();
            var offline = allMembers.Where(m => m.Status == "offline").OrderBy(m => m.DisplayName).ToList();
            CollectionDiffHelper.SyncUsers(OnlineMembers, online);
            CollectionDiffHelper.SyncUsers(OfflineMembers, offline);
            OnPropertyChanged(nameof(OnlineMembersCount));
            OnPropertyChanged(nameof(OfflineMembersCount));
            OnPropertyChanged(nameof(HasOfflineMembers));
            var freshGroups = new List<DiscordMemberGroup>();
            if (_selectedGuild != null && _selectedGuild.Roles != null && _selectedGuild.Roles.Count > 0)
            {
                var hoistRoles = _selectedGuild.Roles
                    .Where(r => r.Hoist)
                    .OrderByDescending(r => r.Position)
                    .ToList();
                var groupedUserIds = new HashSet<string>();
                foreach (var role in hoistRoles)
                {
                    var groupMembers = online
                        .Where(u => u.TopHoistRole?.Id == role.Id)
                        .OrderBy(u => u.DisplayName)
                        .ToList();
                    if (groupMembers.Count > 0)
                    {
                        var group = new DiscordMemberGroup
                        {
                            Name = role.Name,
                            RoleColorHex = role.ColorHex,
                            Position = role.Position
                        };
                        foreach (var u in groupMembers)
                        {
                            group.Members.Add(u);
                            groupedUserIds.Add(u.Id);
                        }
                        group.NotifyMembersChanged();
                        freshGroups.Add(group);
                    }
                }
                var remainingOnline = online.Where(u => !groupedUserIds.Contains(u.Id)).ToList();
                if (remainingOnline.Count > 0)
                {
                    var onlineGroup = new DiscordMemberGroup
                    {
                        Name = "En ligne",
                        RoleColorHex = "#949BA4",
                        Position = -1
                    };
                    foreach (var u in remainingOnline) onlineGroup.Members.Add(u);
                    onlineGroup.NotifyMembersChanged();
                    freshGroups.Add(onlineGroup);
                }
                if (offline.Count > 0)
                {
                    var offlineGroup = new DiscordMemberGroup
                    {
                        Name = "Hors ligne",
                        RoleColorHex = "#949BA4",
                        Position = -2
                    };
                    foreach (var u in offline) offlineGroup.Members.Add(u);
                    offlineGroup.NotifyMembersChanged();
                    freshGroups.Add(offlineGroup);
                }
            }
            else
            {
                if (online.Count > 0)
                {
                    var onlineGroup = new DiscordMemberGroup
                    {
                        Name = "En ligne",
                        RoleColorHex = "#949BA4",
                        Position = 0
                    };
                    foreach (var u in online) onlineGroup.Members.Add(u);
                    onlineGroup.NotifyMembersChanged();
                    freshGroups.Add(onlineGroup);
                }
                if (offline.Count > 0)
                {
                    var offlineGroup = new DiscordMemberGroup
                    {
                        Name = "Hors ligne",
                        RoleColorHex = "#949BA4",
                        Position = -1
                    };
                    foreach (var u in offline) offlineGroup.Members.Add(u);
                    offlineGroup.NotifyMembersChanged();
                    freshGroups.Add(offlineGroup);
                }
            }
            CollectionDiffHelper.SyncMemberGroups(MemberGroups, freshGroups);
            OnPropertyChanged(nameof(HasMemberGroups));
        });
    }
    public void RefreshRecentMemberActivities()
    {
        Protocol.App.RunOnUiThread(() =>
        {
            // Déduplication absolue et préservation de la dernière référence
            var memberDict = new Dictionary<string, DiscordUser>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in ChannelMembers)
            {
                if (m != null && !string.IsNullOrEmpty(m.Id))
                    memberDict[m.Id] = m;
            }
            if (_selectedGuild != null)
            {
                var cached = _client.GetCachedGuildMembers(_selectedGuild.Id);
                foreach (var m in cached)
                {
                    if (m != null && !string.IsNullOrEmpty(m.Id))
                        memberDict[m.Id] = m;
                }
            }
            var allMembers = memberDict.Values.ToList();
            var cards = new List<MemberActivityCardViewModel>();
            var seenCards = new HashSet<string>();
            foreach (var user in allMembers)
            {
                var (_, vcName) = _client.GetUserVoiceChannel(user.Id);
                user.CurrentVoiceChannelName = vcName;
                if (user.Activities != null && user.Activities.Count > 0)
                {
                    foreach (var act in user.Activities.Where(a => a.Type != 4 && a.Type != 6))
                    {
                        var key = $"{user.Id}_{act.Name}";
                        if (seenCards.Add(key))
                        {
                            cards.Add(new MemberActivityCardViewModel(user, act, vcName));
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(vcName))
                {
                    var key = $"{user.Id}_voice";
                    if (seenCards.Add(key))
                    {
                        var voiceAct = new DiscordActivity
                        {
                            Name = "Salon vocal",
                            Details = vcName,
                            Type = 5
                        };
                        cards.Add(new MemberActivityCardViewModel(user, voiceAct, vcName));
                    }
                }
            }
            var sorted = cards
                .OrderByDescending(c => !string.IsNullOrEmpty(c.VoiceChannelName))
                .ThenByDescending(c => c.HasLargeImage)
                .ThenBy(c => c.UserName)
                .ToList();
            RecentMemberActivities.Clear();
            foreach (var card in sorted)
            {
                RecentMemberActivities.Add(card);
            }
            OnPropertyChanged(nameof(HasRecentMemberActivities));
        });
    }
    public async Task BlockUserAsync(DiscordUser user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Id)) return;
        try
        {
            await _client.Rest.PutRelationshipAsync(user.Id, type: 2);
        }
        catch { }
    }
    public void SetUserVoiceVolume(string userId, float volume)
    {
        _client.SetUserVoiceVolume(userId, volume);
        var target = ChannelMembers.FirstOrDefault(u => u.Id == userId)
                     ?? OnlineMembers.FirstOrDefault(u => u.Id == userId)
                     ?? OfflineMembers.FirstOrDefault(u => u.Id == userId)
                     ?? VoiceParticipants.FirstOrDefault(u => u.Id == userId);
        if (target != null)
        {
            target.VoiceVolume = Math.Round(volume * 100);
            target.IsLocalMuted = volume <= 0.001f;
        }
    }
    public void ToggleUserVoiceMute(string userId)
    {
        var target = ChannelMembers.FirstOrDefault(u => u.Id == userId)
                     ?? OnlineMembers.FirstOrDefault(u => u.Id == userId)
                     ?? OfflineMembers.FirstOrDefault(u => u.Id == userId)
                     ?? VoiceParticipants.FirstOrDefault(u => u.Id == userId);
        if (target != null)
        {
            if (target.IsLocalMuted)
            {
                SetUserVoiceVolume(userId, 1.0f);
            }
            else
            {
                SetUserVoiceVolume(userId, 0.0f);
            }
        }
    }
    private void NotifyCurrentUserRichPresenceChanged()
    {
        OnPropertyChanged(nameof(CurrentStatusDisplayName));
        OnPropertyChanged(nameof(CurrentUserSubStatusText));
        OnPropertyChanged(nameof(CurrentUserIsInVoice));
        OnPropertyChanged(nameof(CurrentUserRichPresence));
        OnPropertyChanged(nameof(CurrentUserRichPresenceVisibility));
        OnPropertyChanged(nameof(CurrentUserRpcHeader));
        OnPropertyChanged(nameof(CurrentUserRpcName));
        OnPropertyChanged(nameof(CurrentUserRpcDetails));
        OnPropertyChanged(nameof(CurrentUserRpcDetailsVisibility));
        OnPropertyChanged(nameof(CurrentUserRpcState));
        OnPropertyChanged(nameof(CurrentUserRpcStateVisibility));
        OnPropertyChanged(nameof(CurrentUserRpcElapsed));
        OnPropertyChanged(nameof(CurrentUserRpcElapsedVisibility));
        OnPropertyChanged(nameof(CurrentUserRpcLargeImageUrl));
        OnPropertyChanged(nameof(CurrentUserRpcLargeImageVisibility));
        OnPropertyChanged(nameof(CurrentUserRpcSmallImageUrl));
        OnPropertyChanged(nameof(CurrentUserRpcSmallImageVisibility));
    }
    public void OnUserLoaded()
    {
        CurrentUserName = _client.CurrentUser?.DisplayName ?? "Utilisateur inconnu";
        CurrentUserBadges.Clear();
        if (_client.CurrentUser?.Badges is not null)
        {
            foreach (var b in _client.CurrentUser.Badges) CurrentUserBadges.Add(b);
        }
        if (CurrentUserBadges.All(b => b.Id != "diswin_user"))
        {
            CurrentUserBadges.Insert(0, new DiscordBadge
            {
                Id = "diswin_user",
                Description = "Diswin User",
                Icon = "ms-appx:///Assets/Badges/diswin_user.png",
                Link = "https://github.com/djellil/diswin"
            });
        }
        CurrentUserGameWidgets.Clear();
        if (_client.CurrentUser?.GameWidgets is not null && _client.CurrentUser.GameWidgets.Count > 0)
        {
            foreach (var gw in _client.CurrentUser.GameWidgets) CurrentUserGameWidgets.Add(gw);
        }
        else if (App.Settings.Current.CustomGameWidgets.Count > 0)
        {
            foreach (var gw in App.Settings.Current.CustomGameWidgets) CurrentUserGameWidgets.Add(gw);
        }
        else
        {
            var defaultGames = new List<DiscordWidgetGame>
            {
                new() { GameId = "nfs_heat", Name = "Need for Speed™ Heat", IconUrl = "https://cdn2.steamgriddb.com/grid/9bca6a1fe3b1daea118c8198f1262d22.png", Comment = "Mon jeu de course arcade préféré", IsFavorite = true },
                new() { GameId = "nfs_unbound", Name = "Need for Speed™ Unbound", IconUrl = "https://cdn2.steamgriddb.com/grid/67aa887c2688f19da525f00e95c1a84f.png", Comment = "Style graphique incroyable" },
                new() { GameId = "naruto_shinobi", Name = "Naruto to Boruto: Shinobi Striker", IconUrl = "https://cdn2.steamgriddb.com/grid/0ca53bebc2961d368e7d800fc773d12f.png", Comment = "Combats ninja en équipe" },
                new() { GameId = "apex_legends", Name = "Apex Legends", IconUrl = "https://cdn2.steamgriddb.com/grid/2e40049dfb37c0500e28e4e94b293671.png", Comment = "Battle Royale compétitif" }
            };
            App.Settings.Current.CustomGameWidgets = defaultGames;
            App.Settings.Save();
            foreach (var gw in defaultGames) CurrentUserGameWidgets.Add(gw);
        }
        CurrentUserConnectedAccounts.Clear();
        if (_client.CurrentUser?.ConnectedAccounts is not null)
        {
            foreach (var ca in _client.CurrentUser.ConnectedAccounts) CurrentUserConnectedAccounts.Add(ca);
        }
        OnPropertyChanged(nameof(CurrentUser));
        OnPropertyChanged(nameof(CurrentUserAvatarUrl));
        OnPropertyChanged(nameof(CurrentUserBannerUrl));
        OnPropertyChanged(nameof(CurrentUserHasBanner));
        OnPropertyChanged(nameof(CurrentUserTag));
        OnPropertyChanged(nameof(CurrentUserBio));
        OnPropertyChanged(nameof(CurrentUserPronouns));
        OnPropertyChanged(nameof(CurrentUserPronounsVisibility));
        OnPropertyChanged(nameof(CurrentUserClanTag));
        OnPropertyChanged(nameof(CurrentUserClanBadgeUrl));
        OnPropertyChanged(nameof(CurrentUserClanVisibility));
        OnPropertyChanged(nameof(CurrentUserBadgesVisibility));
        OnPropertyChanged(nameof(CurrentUserGameWidgetsVisibility));
        OnPropertyChanged(nameof(CurrentUserConnectedAccountsVisibility));
        OnPropertyChanged(nameof(CurrentUserFavoriteGame));
        OnPropertyChanged(nameof(CurrentUserFavoriteGameVisibility));
        NotifyCurrentUserRichPresenceChanged();
        RefreshFilteredFriends();
    }
    public async Task EnsureUserProfileEnrichedAsync(DiscordUser user)
    {
        if (user is null || string.IsNullOrWhiteSpace(user.Id)) return;
        user.IsFriend = _client.IsFriend(user.Id);
        if (user.IsFriend)
        {
            var rel = _client.Friends.FirstOrDefault(f => f.User?.Id == user.Id || f.Id == user.Id);
            if (rel != null && ulong.TryParse(rel.Id, out ulong rSnowflake))
            {
                long rTime = (long)(rSnowflake >> 22) + 1420070400000L;
                user.FriendsSinceText = DateTimeOffset.FromUnixTimeMilliseconds(rTime).ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        // Récupérer les activités récentes du cache RPC
        var hist = _client.GetUserActivityHistory(user.Id);
        var acts = new List<DiscordActivity>();
        if (user.Activities != null) acts.AddRange(user.Activities.Where(a => a.Type != 4 && a.Type != 6));
        foreach (var h in hist)
        {
            if (acts.All(a => a.Name != h.Name)) acts.Add(h);
        }
        user.RecentActivities = acts;
        if (!user.HasProfileEnriched)
        {
            await _client.Rest.EnrichUserProfileAsync(user);
            // Re-fusionner avec l'historique d'activités réelles
            var updatedHist = _client.GetUserActivityHistory(user.Id);
            foreach (var h in updatedHist)
            {
                if (acts.All(a => a.Name != h.Name)) acts.Add(h);
            }
            user.RecentActivities = acts;
            // Résoudre les noms et icônes réels des serveurs en commun
            if (user.MutualGuilds != null && user.MutualGuilds.Count > 0)
            {
                foreach (var mg in user.MutualGuilds)
                {
                    var matchGuild = Guilds.FirstOrDefault(g => g.Id == mg.Id);
                    if (matchGuild != null)
                    {
                        mg.Name = matchGuild.Name;
                        if (string.IsNullOrEmpty(mg.IconUrl) && !string.IsNullOrEmpty(matchGuild.IconUrl))
                        {
                            mg.IconUrl = matchGuild.IconUrl;
                        }
                    }
                }
            }
            // Charger les amis en commun si disponibles
            if (user.MutualFriendsCount > 0 && user.MutualFriends.Count == 0)
            {
                var mf = await _client.Rest.GetMutualFriendsAsync(user.Id);
                if (mf.Count > 0)
                {
                    foreach (var friend in mf)
                    {
                        var existingFriend = _client.Friends.FirstOrDefault(f => f.User?.Id == friend.Id)?.User;
                        if (existingFriend != null)
                        {
                            friend.Status = existingFriend.Status;
                            friend.Activities = existingFriend.Activities;
                            if (existingFriend.Clan != null && friend.Clan == null) friend.Clan = existingFriend.Clan;
                            if (existingFriend.PrimaryGuild != null && friend.PrimaryGuild == null) friend.PrimaryGuild = existingFriend.PrimaryGuild;
                        }
                        else
                        {
                            _client.ApplyCachedPresenceToUser(friend);
                        }
                        friend.NotifyPresenceChanged();
                    }
                    user.MutualFriends = mf;
                }
            }
            user.NotifyProfileEnriched();
        }
    }
    [ObservableProperty]
    private string friendsTab = "Online";
    partial void OnFriendsTabChanged(string value)
    {
        _friendsFilter = value;
        RefreshFilteredFriends();
        OnPropertyChanged(nameof(IsFriendsTabOnline));
        OnPropertyChanged(nameof(IsFriendsTabAll));
        OnPropertyChanged(nameof(IsFriendsTabPending));
        OnPropertyChanged(nameof(IsFriendsTabBlocked));
        OnPropertyChanged(nameof(IsFriendsTabAddFriend));
        OnPropertyChanged(nameof(FriendsTabOnlineBackground));
        OnPropertyChanged(nameof(FriendsTabAllBackground));
        OnPropertyChanged(nameof(FriendsTabPendingBackground));
        OnPropertyChanged(nameof(FriendsTabBlockedBackground));
        OnPropertyChanged(nameof(FriendsTabAddFriendBackground));
        OnPropertyChanged(nameof(FriendsTabOnlineForeground));
        OnPropertyChanged(nameof(FriendsTabAllForeground));
        OnPropertyChanged(nameof(FriendsTabPendingForeground));
        OnPropertyChanged(nameof(FriendsTabBlockedForeground));
        OnPropertyChanged(nameof(FriendsTabAddFriendForeground));
        OnPropertyChanged(nameof(FriendsListVisibility));
        OnPropertyChanged(nameof(AddFriendStageVisibility));
        OnPropertyChanged(nameof(FriendsSectionTitleText));
    }
    public bool IsFriendsTabOnline => FriendsTab == "Online";
    public bool IsFriendsTabAll => FriendsTab == "All";
    public bool IsFriendsTabPending => FriendsTab == "Pending";
    public bool IsFriendsTabBlocked => FriendsTab == "Blocked";
    public bool IsFriendsTabAddFriend => FriendsTab == "AddFriend";
    public Microsoft.UI.Xaml.Visibility FriendsListVisibility => FriendsTab != "AddFriend" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public Microsoft.UI.Xaml.Visibility AddFriendStageVisibility => FriendsTab == "AddFriend" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush TabActiveBgBrush = new(Windows.UI.Color.FromArgb(255, 53, 55, 60)); // #35373C
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush TabTransparentBgBrush = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush TabActiveFgBrush = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush TabInactiveFgBrush = new(Windows.UI.Color.FromArgb(255, 148, 155, 164)); // #949BA4
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush AddFriendActiveBgBrush = new(Windows.UI.Color.FromArgb(255, 35, 165, 90)); // #23A55A
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush AddFriendInactiveFgBrush = new(Windows.UI.Color.FromArgb(255, 35, 165, 90));
    public Microsoft.UI.Xaml.Media.Brush FriendsTabOnlineBackground => IsFriendsTabOnline ? TabActiveBgBrush : TabTransparentBgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabAllBackground => IsFriendsTabAll ? TabActiveBgBrush : TabTransparentBgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabPendingBackground => IsFriendsTabPending ? TabActiveBgBrush : TabTransparentBgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabBlockedBackground => IsFriendsTabBlocked ? TabActiveBgBrush : TabTransparentBgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabAddFriendBackground => IsFriendsTabAddFriend ? AddFriendActiveBgBrush : TabTransparentBgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabOnlineForeground => IsFriendsTabOnline ? TabActiveFgBrush : TabInactiveFgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabAllForeground => IsFriendsTabAll ? TabActiveFgBrush : TabInactiveFgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabPendingForeground => IsFriendsTabPending ? TabActiveFgBrush : TabInactiveFgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabBlockedForeground => IsFriendsTabBlocked ? TabActiveFgBrush : TabInactiveFgBrush;
    public Microsoft.UI.Xaml.Media.Brush FriendsTabAddFriendForeground => IsFriendsTabAddFriend ? TabActiveFgBrush : AddFriendInactiveFgBrush;
    [ObservableProperty]
    private string friendsSearchQuery = string.Empty;
    partial void OnFriendsSearchQueryChanged(string value)
    {
        RefreshFilteredFriends();
    }
    [ObservableProperty]
    private string addFriendInput = string.Empty;
    [ObservableProperty]
    private string addFriendFeedbackMessage = string.Empty;
    [ObservableProperty]
    private bool hasAddFriendFeedback;
    [ObservableProperty]
    private bool isAddFriendSuccess;
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush FeedbackSuccessBgBrush = new(Windows.UI.Color.FromArgb(255, 20, 50, 30));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush FeedbackErrorBgBrush = new(Windows.UI.Color.FromArgb(255, 50, 20, 20));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush FeedbackSuccessFgBrush = new(Windows.UI.Color.FromArgb(255, 35, 165, 90));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush FeedbackErrorFgBrush = new(Windows.UI.Color.FromArgb(255, 242, 63, 67));
    public Microsoft.UI.Xaml.Media.Brush AddFriendFeedbackBackground => IsAddFriendSuccess ? FeedbackSuccessBgBrush : FeedbackErrorBgBrush;
    public Microsoft.UI.Xaml.Media.Brush AddFriendFeedbackForeground => IsAddFriendSuccess ? FeedbackSuccessFgBrush : FeedbackErrorFgBrush;
    public ObservableCollection<DiscordRelationship> ActiveNowFriends { get; } = new();
    public bool HasActiveNowFriends => ActiveNowFriends.Count > 0;
    public int OnlineFriendsCount => _client.Friends.Count(r => r.IsOnline);
    public int AllFriendsCount => _client.Friends.Count(r => r.IsFriend);
    public int PendingFriendsCount => _client.Friends.Count(r => r.IsPending);
    public int BlockedFriendsCount => _client.Friends.Count(r => r.IsBlocked);
    public bool HasPendingFriends => PendingFriendsCount > 0;
    public string PendingBadgeText => PendingFriendsCount.ToString();
    public string FriendsSectionTitleText => FriendsTab switch
    {
        "Online" => $"EN LIGNE — {FilteredFriends.Count}",
        "All" => $"TOUS LES AMIS — {FilteredFriends.Count}",
        "Pending" => $"EN ATTENTE — {FilteredFriends.Count}",
        "Blocked" => $"BLOQUÉS — {FilteredFriends.Count}",
        "AddFriend" => "AJOUTER UN AMI",
        _ => $"AMIS — {FilteredFriends.Count}"
    };
    public void SetFriendsFilter(string filter)
    {
        _friendsFilter = filter;
        if (FriendsTab != filter) FriendsTab = filter;
        RefreshFilteredFriends();
    }
    public void RefreshFilteredFriends()
    {
        FilteredFriends.Clear();
        ActiveNowFriends.Clear();
        IEnumerable<DiscordRelationship> query = _client.Friends;
        // Populate ActiveNowFriends
        foreach (var r in query.Where(r => r.IsOnline && (r.User.HasActivityOrCustomStatus || r.User.HasGameActivity)))
        {
            ActiveNowFriends.Add(r);
        }
        OnPropertyChanged(nameof(HasActiveNowFriends));
        var filter = FriendsTab;
        query = filter switch
        {
            "Online" => query.Where(r => r.IsOnline),
            "Pending" => query.Where(r => r.IsPending),
            "Blocked" => query.Where(r => r.IsBlocked),
            _ => query.Where(r => r.IsFriend),
        };
        if (!string.IsNullOrWhiteSpace(FriendsSearchQuery))
        {
            var q = FriendsSearchQuery.Trim();
            query = query.Where(r =>
                r.User.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.User.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.User.UserTag.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        foreach (var rel in query)
        {
            FilteredFriends.Add(rel);
        }
        OnPropertyChanged(nameof(OnlineFriendsCount));
        OnPropertyChanged(nameof(AllFriendsCount));
        OnPropertyChanged(nameof(PendingFriendsCount));
        OnPropertyChanged(nameof(BlockedFriendsCount));
        OnPropertyChanged(nameof(HasPendingFriends));
        OnPropertyChanged(nameof(PendingBadgeText));
        OnPropertyChanged(nameof(FriendsSectionTitleText));
    }
    public async Task SubmitAddFriendAsync()
    {
        if (string.IsNullOrWhiteSpace(AddFriendInput)) return;
        try
        {
            await SendFriendRequestAsync(AddFriendInput.Trim());
            AddFriendFeedbackMessage = $"Succès ! Ta demande d'ami a été envoyée à {AddFriendInput.Trim()}.";
            IsAddFriendSuccess = true;
            HasAddFriendFeedback = true;
            OnPropertyChanged(nameof(AddFriendFeedbackBackground));
            OnPropertyChanged(nameof(AddFriendFeedbackForeground));
            AddFriendInput = string.Empty;
        }
        catch (Exception ex)
        {
            AddFriendFeedbackMessage = $"Hm, ça n'a pas fonctionné. Vérifie que l'orthographe est correcte. ({ex.Message})";
            IsAddFriendSuccess = false;
            HasAddFriendFeedback = true;
            OnPropertyChanged(nameof(AddFriendFeedbackBackground));
            OnPropertyChanged(nameof(AddFriendFeedbackForeground));
        }
    }
    public void ToggleMemberList()
    {
        IsMemberListOpen = !IsMemberListOpen;
        OnPropertyChanged(nameof(MemberListVisibility));
    }
    public async Task UpdateProfileAsync(
        string? newDisplayName = null,
        string? newBio = null,
        string? newPronouns = null,
        string? avatarDataUri = null,
        bool removeAvatar = false,
        string? bannerDataUri = null,
        bool removeBanner = false,
        int? accentColor = null,
        string? customStatusText = null,
        string? newUsername = null,
        string? currentPassword = null,
        string? newPassword = null)
    {
        try
        {
            if (customStatusText is not null)
            {
                await _client.Rest.UpdateCustomStatusAsync(customStatusText);
                if (!string.IsNullOrWhiteSpace(customStatusText))
                {
                    CurrentStatus = customStatusText;
                }
            }
            var updated = await _client.Rest.ModifyCurrentUserAsync(
                globalName: newDisplayName,
                username: newUsername,
                currentPassword: currentPassword,
                newPassword: newPassword,
                bio: newBio,
                pronouns: newPronouns,
                avatarDataUri: avatarDataUri,
                removeAvatar: removeAvatar,
                bannerDataUri: bannerDataUri,
                removeBanner: removeBanner,
                accentColor: accentColor);
            _client.CurrentUser = updated;
            OnUserLoaded();
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] UpdateProfileAsync failed: {ex}\n\n");
            throw;
        }
    }
    public async Task<(string? nick, string? bio, string? pronouns, string? avatarHash, string? bannerHash)> GetGuildMemberProfileAsync(string guildId)
    {
        if (_client.CurrentUser is null || string.IsNullOrWhiteSpace(guildId)) return (null, null, null, null, null);
        return await _client.Rest.GetGuildMemberProfileAsync(guildId, _client.CurrentUser.Id);
    }
    public async Task UpdateGuildMemberProfileAsync(
        string guildId,
        string? nick = null,
        string? bio = null,
        string? pronouns = null,
        string? avatarDataUri = null,
        bool removeAvatar = false,
        string? bannerDataUri = null,
        bool removeBanner = false,
        int? accentColor = null)
    {
        if (string.IsNullOrWhiteSpace(guildId)) return;
        await _client.Rest.ModifyGuildMemberProfileAsync(
            guildId,
            nick: nick,
            bio: bio,
            pronouns: pronouns,
            avatarDataUri: avatarDataUri,
            removeAvatar: removeAvatar,
            bannerDataUri: bannerDataUri,
            removeBanner: removeBanner,
            accentColor: accentColor);
    }
    public void ShowFriendsView()
    {
        IsDiscoveryOpen = false;
        if (_selectedGuild != null)
        {
            _selectedGuild.IsSelected = false;
            _selectedGuild = null;
        }
        _client.ActiveGuildId = null;
        _showingFriends = true;
        _selectedChannel = null;
        SelectedChannelName = "Amis & Messages Privés";
        SelectedChannelTopic = string.Empty;
        OnPropertyChanged(nameof(SelectedChannel));
        OnPropertyChanged(nameof(ShowingGuildChannels));
        OnPropertyChanged(nameof(ShowingFriendsView));
        OnPropertyChanged(nameof(ShowingFriendsMainStage));
        OnPropertyChanged(nameof(FriendsMainStageVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
        OnPropertyChanged(nameof(SelectedGuild));
        OnPropertyChanged(nameof(HasSelectedGuild));
        OnPropertyChanged(nameof(HasSelectedGuildBanner));
        OnPropertyChanged(nameof(SelectedGuildBannerUrl));
        OnPropertyChanged(nameof(SelectedGuildBoostBadge));
        OnPropertyChanged(nameof(HasSelectedGuildBoosts));
        RefreshFilteredFriends();
    }
    public async Task SelectGuildAsync(DiscordGuild guild)
    {
        IsDiscoveryOpen = false;
        if (_selectedGuild != null && _selectedGuild != guild)
        {
            _selectedGuild.IsSelected = false;
        }
        _showingFriends = false;
        OnPropertyChanged(nameof(ShowingGuildChannels));
        OnPropertyChanged(nameof(ShowingFriendsView));
        OnPropertyChanged(nameof(ShowingFriendsMainStage));
        OnPropertyChanged(nameof(FriendsMainStageVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
        _selectedGuild = guild;
        guild.IsSelected = true;
        _client.ActiveGuildId = guild.Id;
        SelectedGuildName = guild.Name;
        OnPropertyChanged(nameof(SelectedGuild));
        OnPropertyChanged(nameof(HasSelectedGuild));
        OnPropertyChanged(nameof(HasSelectedGuildBanner));
        OnPropertyChanged(nameof(SelectedGuildBannerUrl));
        OnPropertyChanged(nameof(SelectedGuildBoostBadge));
        OnPropertyChanged(nameof(HasSelectedGuildBoosts));
        Channels.Clear();
        // Demander à la Gateway de s'abonner au serveur (Opcode 14) pour recevoir les mises à jour vocales et les membres en temps réel
        _ = _client.SubscribeGuildAsync(guild.Id);
        List<DiscordChannel> rawChannels;
        if (guild.Channels.Count > 0)
        {
            rawChannels = guild.Channels;
        }
        else
        {
            try
            {
                var fetchedChannels = await _client.Rest.GetGuildChannelsAsync(guild.Id);
                guild.Channels = fetchedChannels;
                rawChannels = fetchedChannels;
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                    $"[{DateTime.Now}] GetGuildChannelsAsync failed: {ex}\n\n");
                rawChannels = new List<DiscordChannel>();
            }
        }
        foreach (var ch in rawChannels.Where(c => c.IsVoiceChannel))
        {
            _client.RegisterGuildVoiceChannel(guild.Id, ch.Id);
        }
        _client.UpdateGuildVoiceAndStreamStatus(guild.Id);
        // Calculer les permissions de chaque salon (visibilité et connexion)
        var userRoles = _client.GetCurrentUserGuildRoles(guild.Id);
        var channelDict = rawChannels.ToDictionary(c => c.Id);
        foreach (var ch in rawChannels)
        {
            var (canView, canConnect) = DiscordPermissionEvaluator.EvaluateChannelPermissions(
                ch,
                guild,
                _client.CurrentUser?.Id,
                userRoles,
                channelDict);
            ch.CanView = canView;
            ch.CanConnect = canConnect;
        }
        bool showHidden = App.Settings.Current.ShowHiddenChannels;
        // Groupement hiérarchique par catégories (Discord 1:1)
        var categoryChannels = rawChannels
            .Where(c => c.Type == DiscordChannelType.GuildCategory)
            .OrderBy(c => c.Position)
            .ToList();
        var uncategorized = rawChannels
            .Where(c => (c.IsTextLike || c.IsVoiceChannel) && string.IsNullOrEmpty(c.ParentId) && (c.CanView || showHidden))
            .OrderBy(c => c.IsVoiceChannel ? 1 : 0)
            .ThenBy(c => c.Position)
            .ToList();
        void AddChannelWithUnreadCheck(DiscordChannel ch)
        {
            ch.GuildId ??= guild.Id;
            if (!string.IsNullOrEmpty(ch.LastMessageId) && _client.ReadLastMessages.TryGetValue(ch.Id, out var rMid))
            {
                if (ulong.TryParse(ch.LastMessageId, out var cMid) &&
                    ulong.TryParse(rMid, out var readMid) &&
                    cMid > readMid)
                {
                    ch.HasUnread = true;
                }
            }
            Channels.Add(ch);
        }
        // 1. Canaux non catégorisés d'abord
        foreach (var ch in uncategorized)
        {
            AddChannelWithUnreadCheck(ch);
        }
        // 2. Chaque catégorie avec ses salons enfants
        foreach (var cat in categoryChannels)
        {
            cat.GuildId ??= guild.Id;
            Channels.Add(cat);
            var children = rawChannels
                .Where(c => (c.IsTextLike || c.IsVoiceChannel) && c.ParentId == cat.Id && (c.CanView || showHidden))
                .OrderBy(c => c.IsVoiceChannel ? 1 : 0)
                .ThenBy(c => c.Position);
            foreach (var child in children)
            {
                AddChannelWithUnreadCheck(child);
            }
        }
        // S'il n'y avait aucune catégorie sur le serveur, s'assurer que tous les salons sont ajoutés
        if (categoryChannels.Count == 0 && uncategorized.Count == 0)
        {
            foreach (var ch in rawChannels.Where(c => (c.IsTextLike || c.IsVoiceChannel) && (c.CanView || showHidden)))
            {
                AddChannelWithUnreadCheck(ch);
            }
        }
        foreach (var ch in Channels.Where(c => c.IsVoiceChannel))
        {
            SyncChannelVoiceMembers(ch);
        }
        FilteredChannels.Clear();
        foreach (var ch in Channels) FilteredChannels.Add(ch);
        SyncOnlineOfflineMembers();
        RefreshRecentMemberActivities();
    }
    public void RefreshDisplayGuilds()
    {
        Protocol.App.RunOnUiThread(() =>
        {
            var allGuilds = _client.Guilds.ToList();
            var folders = _client.GuildFolders;
            var assignedGuildIds = new HashSet<string>();
            var existingStates = new Dictionary<string, bool>();
            foreach (var item in DisplayGuilds.Where(g => g.IsFolder))
            {
                existingStates[item.Id] = item.IsFolderCollapsed;
            }
            DisplayGuilds.Clear();
            if (folders != null && folders.Count > 0)
            {
                foreach (var folderData in folders)
                {
                    var folderGuilds = allGuilds.Where(g => folderData.GuildIds.Contains(g.Id)).ToList();
                    if (folderGuilds.Count == 0) continue;
                    foreach (var g in folderGuilds) assignedGuildIds.Add(g.Id);
                    if (folderData.IsFolder)
                    {
                        var folderId = $"folder_{folderData.IdString ?? Guid.NewGuid().ToString("N")}";
                        bool isCollapsed = true;
                        if (existingStates.TryGetValue(folderId, out var prevCollapsed))
                        {
                            isCollapsed = prevCollapsed;
                        }
                        var folderItem = new DiscordGuild
                        {
                            Id = folderId,
                            Name = folderData.Name ?? "Dossier de serveurs",
                            IsFolder = true,
                            FolderColor = folderData.Color,
                            IsFolderCollapsed = isCollapsed
                        };
                        folderItem.FolderChildren.Clear();
                        foreach (var g in folderGuilds)
                        {
                            g.FolderId = folderItem.Id;
                            folderItem.FolderChildren.Add(g);
                        }
                        folderItem.NotifyFolderChanged();
                        DisplayGuilds.Add(folderItem);
                        if (!isCollapsed)
                        {
                            foreach (var g in folderGuilds)
                            {
                                DisplayGuilds.Add(g);
                            }
                        }
                    }
                    else
                    {
                        foreach (var g in folderGuilds)
                        {
                            g.FolderId = null;
                            DisplayGuilds.Add(g);
                        }
                    }
                }
            }
            foreach (var g in allGuilds)
            {
                if (!assignedGuildIds.Contains(g.Id))
                {
                    g.FolderId = null;
                    DisplayGuilds.Add(g);
                }
            }
        });
    }
    public void ToggleFolderCollapse(DiscordGuild folder)
    {
        if (!folder.IsFolder) return;
        folder.IsFolderCollapsed = !folder.IsFolderCollapsed;
        int folderIndex = DisplayGuilds.IndexOf(folder);
        if (folderIndex < 0) return;
        if (folder.IsFolderCollapsed)
        {
            foreach (var child in folder.FolderChildren)
            {
                DisplayGuilds.Remove(child);
            }
        }
        else
        {
            for (int i = 0; i < folder.FolderChildren.Count; i++)
            {
                DisplayGuilds.Insert(folderIndex + 1 + i, folder.FolderChildren[i]);
            }
        }
    }
    public void MarkFolderRead(DiscordGuild folder)
    {
        if (!folder.IsFolder) return;
        foreach (var child in folder.FolderChildren)
        {
            child.MentionCount = 0;
            child.HasUnread = false;
        }
        folder.NotifyFolderChanged();
    }
    public void CreateLocalServerFolder(string folderName, DiscordGuild firstGuild)
    {
        if (firstGuild == null) return;
        var folderItem = new DiscordGuild
        {
            Id = $"folder_local_{Guid.NewGuid():N}",
            Name = string.IsNullOrWhiteSpace(folderName) ? "Nouveau dossier" : folderName.Trim(),
            IsFolder = true,
            IsFolderCollapsed = false
        };
        firstGuild.FolderId = folderItem.Id;
        folderItem.FolderChildren.Add(firstGuild);
        folderItem.NotifyFolderChanged();
        int idx = DisplayGuilds.IndexOf(firstGuild);
        if (idx >= 0)
        {
            DisplayGuilds.Insert(idx, folderItem);
        }
        else
        {
            DisplayGuilds.Add(folderItem);
            DisplayGuilds.Add(firstGuild);
        }
    }
    public void DissolveFolder(DiscordGuild folder)
    {
        if (!folder.IsFolder) return;
        int idx = DisplayGuilds.IndexOf(folder);
        if (idx < 0) return;
        DisplayGuilds.RemoveAt(idx);
        if (folder.IsFolderCollapsed)
        {
            for (int i = 0; i < folder.FolderChildren.Count; i++)
            {
                folder.FolderChildren[i].FolderId = null;
                DisplayGuilds.Insert(idx + i, folder.FolderChildren[i]);
            }
        }
        else
        {
            foreach (var child in folder.FolderChildren)
            {
                child.FolderId = null;
            }
        }
    }
    public void ToggleCategoryCollapse(DiscordChannel category)
    {
        if (!category.IsCategory) return;
        category.IsCollapsed = !category.IsCollapsed;
        bool isCollapsed = category.IsCollapsed;
        var children = Channels.Where(c => c.ParentId == category.Id).ToList();
        foreach (var ch in children)
        {
            ch.IsCategoryVisible = !isCollapsed;
        }
        if (string.IsNullOrWhiteSpace(SearchFilterText))
        {
            if (isCollapsed)
            {
                foreach (var child in children)
                {
                    FilteredChannels.Remove(child);
                }
            }
            else
            {
                int catIndex = FilteredChannels.IndexOf(category);
                if (catIndex >= 0)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        FilteredChannels.Insert(catIndex + 1 + i, children[i]);
                    }
                }
            }
        }
    }
    public async Task SelectChannelAsync(DiscordChannel channel)
    {
        if (channel.IsCategory)
        {
            ToggleCategoryCollapse(channel);
            return;
        }
        if (channel.IsVoiceChannel && channel.IsLocked)
        {
            return;
        }
        _selectedChannel = channel;
        _client.ActiveChannelId = channel.Id;
        channel.MentionCount = 0;
        channel.HasUnread = false;
        if (_selectedGuild != null && _selectedGuild.MentionCount > 0)
        {
            _selectedGuild.MentionCount = Math.Max(0, _selectedGuild.MentionCount - 1);
        }
        NotifyDmMentionsChanged();
        OnPropertyChanged(nameof(SelectedChannel));
        OnPropertyChanged(nameof(ShowingFriendsMainStage));
        OnPropertyChanged(nameof(FriendsMainStageVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
        SelectedChannelName = channel.DisplayName;
        SelectedChannelTopic = channel.Topic ?? string.Empty;
        OnPropertyChanged(nameof(TopicVisibility));
        CancelComposerAction();
        Messages.Clear();
        ChannelMembers.Clear();
        if (ActiveSplitThread != null && ActiveSplitThread.ParentId != channel.Id)
        {
            CloseSplitViewThread();
        }
        _ = LoadActiveChannelThreadsAsync();
        var guildIdForSub = channel.GuildId ?? _selectedGuild?.Id;
        if (!string.IsNullOrEmpty(guildIdForSub))
        {
            _ = _client.SubscribeGuildAsync(guildIdForSub, channel.Id);
        }
        if (channel.Type == DiscordChannelType.GuildVoice)
        {
            IsVoiceChannelSelected = true;
            IsForumChannelSelected = false;
            OnPropertyChanged(nameof(VoiceViewVisibility));
            OnPropertyChanged(nameof(TextViewVisibility));
            OnPropertyChanged(nameof(ForumViewVisibility));
            OnPropertyChanged(nameof(MemberListVisibility));
            var targetGuildId = channel.GuildId ?? _selectedGuild?.Id;
            if (targetGuildId != null)
            {
                bool wasConnected = IsVoiceConnected;
                channel.GuildId ??= targetGuildId;
                _ = _client.JoinVoiceChannelAsync(targetGuildId, channel.Id, IsMuted, IsDeafened, IsCameraEnabled, IsScreenSharing);
                IsVoiceConnected = true;
                ConnectedVoiceChannelName = channel.DisplayName;
                if (!wasConnected)
                {
                    AudioSfxService.Play(DiscordSfx.Join);
                }
            }
            RefreshVoiceParticipants(channel.Id);
        }
        else if (channel.Type == DiscordChannelType.GuildForum)
        {
            IsVoiceChannelSelected = false;
            IsForumChannelSelected = true;
            OnPropertyChanged(nameof(VoiceViewVisibility));
            OnPropertyChanged(nameof(TextViewVisibility));
            OnPropertyChanged(nameof(ForumViewVisibility));
            OnPropertyChanged(nameof(MemberListVisibility));
            _ = LoadForumPostsAsync(channel.Id);
        }
        else
        {
            IsVoiceChannelSelected = false;
            IsForumChannelSelected = false;
            OnPropertyChanged(nameof(VoiceViewVisibility));
            OnPropertyChanged(nameof(TextViewVisibility));
            OnPropertyChanged(nameof(ForumViewVisibility));
            OnPropertyChanged(nameof(MemberListVisibility));
            try
            {
                var history = await _client.Rest.GetMessagesAsync(channel.Id, limit: 50);
                DiscordMessage? prev = null;
                var seenUsers = new HashSet<string>();
                if (_client.CurrentUser is not null && seenUsers.Add(_client.CurrentUser.Id))
                {
                    ChannelMembers.Add(_client.CurrentUser);
                }
                if (channel.Recipients is not null)
                {
                    foreach (var r in channel.Recipients)
                    {
                        _client.ApplyCachedPresenceToUser(r);
                        if (seenUsers.Add(r.Id)) ChannelMembers.Add(r);
                    }
                }
                foreach (var message in history.OrderBy(m => m.Timestamp))
                {
                    _client.ApplyCachedPresenceToUser(message.Author);
                    ProcessMessageForGrouping(message, prev);
                    Messages.Add(message);
                    if (seenUsers.Add(message.Author.Id))
                    {
                        ChannelMembers.Add(message.Author);
                    }
                    prev = message;
                }
            }
            catch (Exception)
            {
            }
        }
        SyncOnlineOfflineMembers();
        RefreshRecentMemberActivities();
    }
    private bool _isLoadingOlderMessages;
    public async Task<int> LoadOlderMessagesAsync()
    {
        if (_selectedChannel is null || _isLoadingOlderMessages) return 0;
        var oldest = Messages.FirstOrDefault();
        if (oldest is null) return 0;
        _isLoadingOlderMessages = true;
        try
        {
            var older = await _client.Rest.GetMessagesAsync(_selectedChannel.Id, limit: 50, beforeId: oldest.Id);
            if (older.Count == 0) return 0;
            var ordered = older.OrderBy(m => m.Timestamp).ToList();
            DiscordMessage? prev = null;
            var seenUsers = new HashSet<string>(ChannelMembers.Select(u => u.Id));
            // Insérer au début de la collection
            for (int i = 0; i < ordered.Count; i++)
            {
                var msg = ordered[i];
                ProcessMessageForGrouping(msg, prev);
                Messages.Insert(i, msg);
                if (seenUsers.Add(msg.Author.Id))
                {
                    ChannelMembers.Add(msg.Author);
                }
                prev = msg;
            }
            // Mettre à jour l'en-tête du message qui était précédemment le premier
            if (Messages.Count > ordered.Count)
            {
                ProcessMessageForGrouping(Messages[ordered.Count], prev);
            }
            return older.Count;
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] LoadOlderMessagesAsync failed: {ex}\n\n");
            return 0;
        }
        finally
        {
            _isLoadingOlderMessages = false;
        }
    }
    public async Task OpenThreadInSplitViewAsync(DiscordChannel thread)
    {
        ActiveSplitThread = thread;
        IsSplitThreadOpen = true;
        OnPropertyChanged(nameof(SplitThreadPanelVisibility));
        ActiveThreadMessages.Clear();
        try
        {
            var history = await _client.Rest.GetMessagesAsync(thread.Id, limit: 50);
            DiscordMessage? prev = null;
            foreach (var message in history.OrderBy(m => m.Timestamp))
            {
                _client.ApplyCachedPresenceToUser(message.Author);
                ProcessMessageForGrouping(message, prev);
                ActiveThreadMessages.Add(message);
                prev = message;
            }
        }
        catch { }
    }
    public void CloseSplitViewThread()
    {
        IsSplitThreadOpen = false;
        ActiveSplitThread = null;
        ActiveThreadMessages.Clear();
        OnPropertyChanged(nameof(SplitThreadPanelVisibility));
    }
    public async Task LoadActiveChannelThreadsAsync()
    {
        if (_selectedChannel == null) return;
        try
        {
            var threads = await _client.Rest.GetActiveThreadsAsync(_selectedChannel.Id);
            Protocol.App.RunOnUiThread(() =>
            {
                ChannelActiveThreads.Clear();
                foreach (var t in threads)
                {
                    ChannelActiveThreads.Add(t);
                }
                OnPropertyChanged(nameof(HasChannelActiveThreads));
                OnPropertyChanged(nameof(ChannelActiveThreadsCountText));
            });
        }
        catch { }
    }
    public async Task<bool> SendThreadMessageAsync(string content)
    {
        if (ActiveSplitThread == null || string.IsNullOrWhiteSpace(content)) return false;
        try
        {
            var sent = await _client.Rest.SendMessageAsync(ActiveSplitThread.Id, content);
            if (sent != null)
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    _client.ApplyCachedPresenceToUser(sent.Author);
                    ProcessMessageForGrouping(sent, ActiveThreadMessages.LastOrDefault());
                    ActiveThreadMessages.Add(sent);
                });
                return true;
            }
        }
        catch { }
        return false;
    }
    public async Task<DiscordChannel?> CreateThreadFromMessageAsync(DiscordMessage message, string threadName)
    {
        try
        {
            var thread = await _client.Rest.CreateThreadFromMessageAsync(message.ChannelId, message.Id, threadName);
            if (thread != null)
            {
                message.Thread = thread;
                message.OnPropertyChanged(nameof(message.HasThread));
                Protocol.App.RunOnUiThread(() =>
                {
                    if (ChannelActiveThreads.All(t => t.Id != thread.Id))
                    {
                        ChannelActiveThreads.Insert(0, thread);
                    }
                    OnPropertyChanged(nameof(HasChannelActiveThreads));
                    OnPropertyChanged(nameof(ChannelActiveThreadsCountText));
                });
                await OpenThreadInSplitViewAsync(thread);
                return thread;
            }
        }
        catch { }
        return null;
    }
    public async Task<DiscordChannel?> CreateChannelThreadAsync(string threadName)
    {
        if (_selectedChannel == null) return null;
        try
        {
            var thread = await _client.Rest.CreateThreadAsync(_selectedChannel.Id, threadName);
            if (thread != null)
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    ChannelActiveThreads.Insert(0, thread);
                    OnPropertyChanged(nameof(HasChannelActiveThreads));
                    OnPropertyChanged(nameof(ChannelActiveThreadsCountText));
                });
                await OpenThreadInSplitViewAsync(thread);
                return thread;
            }
        }
        catch { }
        return null;
    }
    public async Task LoadForumPostsAsync(string channelId)
    {
        try
        {
            Protocol.App.RunOnUiThread(() =>
            {
                ForumPosts.Clear();
                FilteredForumPosts.Clear();
                ForumAvailableTags.Clear();
                if (_selectedChannel?.AvailableTags != null)
                {
                    foreach (var tag in _selectedChannel.AvailableTags)
                    {
                        ForumAvailableTags.Add(tag);
                    }
                }
            });
            var active = await _client.Rest.GetActiveThreadsAsync(channelId);
            var archived = await _client.Rest.GetArchivedThreadsAsync(channelId);
            var all = active.Concat(archived)
                            .GroupBy(t => t.Id)
                            .Select(g => g.First())
                            .OrderByDescending(t => t.ThreadMetadata?.ArchiveTimestamp ?? DateTimeOffset.MinValue)
                            .ToList();
            Protocol.App.RunOnUiThread(() =>
            {
                ForumPosts.Clear();
                foreach (var post in all)
                {
                    post.ResolvedTags.Clear();
                    if (_selectedChannel?.AvailableTags != null && post.AppliedTags != null)
                    {
                        foreach (var tagId in post.AppliedTags)
                        {
                            var tag = _selectedChannel.AvailableTags.FirstOrDefault(t => t.Id == tagId);
                            if (tag != null) post.ResolvedTags.Add(tag);
                        }
                    }
                    ForumPosts.Add(post);
                }
                ApplyForumFilter();
            });
        }
        catch { }
    }
    public void ApplyForumFilter()
    {
        FilteredForumPosts.Clear();
        var query = ForumSearchQuery?.Trim();
        var tagId = SelectedForumFilterTag?.Id;
        foreach (var post in ForumPosts)
        {
            if (!string.IsNullOrEmpty(query) && !post.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!string.IsNullOrEmpty(tagId) && (post.AppliedTags == null || !post.AppliedTags.Contains(tagId)))
            {
                continue;
            }
            FilteredForumPosts.Add(post);
        }
        OnPropertyChanged(nameof(HasFilteredForumPosts));
        OnPropertyChanged(nameof(ForumPostsCountText));
    }
    public async Task<DiscordChannel?> CreateForumPostAsync(string title, string content, List<string>? tags = null)
    {
        if (_selectedChannel == null || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            var post = await _client.Rest.CreateForumPostAsync(_selectedChannel.Id, title, content, tags);
            if (post != null)
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    post.ResolvedTags.Clear();
                    if (_selectedChannel?.AvailableTags != null && post.AppliedTags != null)
                    {
                        foreach (var tagId in post.AppliedTags)
                        {
                            var tag = _selectedChannel.AvailableTags.FirstOrDefault(t => t.Id == tagId);
                            if (tag != null) post.ResolvedTags.Add(tag);
                        }
                    }
                    ForumPosts.Insert(0, post);
                    ApplyForumFilter();
                    IsCreatingForumPost = false;
                });
                await OpenThreadInSplitViewAsync(post);
                return post;
            }
        }
        catch { }
        return null;
    }
    public async Task OpenDiscoveryViewAsync()
    {
        if (_selectedGuild != null)
        {
            _selectedGuild.IsSelected = false;
            _selectedGuild = null;
            OnPropertyChanged(nameof(SelectedGuild));
            OnPropertyChanged(nameof(HasSelectedGuild));
        }
        _showingFriends = false;
        OnPropertyChanged(nameof(ShowingGuildChannels));
        OnPropertyChanged(nameof(ShowingFriendsView));
        IsDiscoveryOpen = true;
        await LoadDiscoverableGuildsAsync();
    }
    public void CloseDiscoveryView()
    {
        IsDiscoveryOpen = false;
        ShowFriendsView();
    }
    public async Task LoadDiscoverableGuildsAsync(string? query = null, int category = 0)
    {
        IsLoadingDiscovery = true;
        try
        {
            var results = await _client.Rest.GetDiscoverableGuildsAsync(query, category);
            Protocol.App.RunOnUiThread(() =>
            {
                DiscoverableGuilds.Clear();
                foreach (var g in results)
                {
                    DiscoverableGuilds.Add(g);
                }
                ApplyDiscoveryFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoadDiscoverableGuildsAsync] Error: {ex.Message}");
        }
        finally
        {
            Protocol.App.RunOnUiThread(() =>
            {
                IsLoadingDiscovery = false;
            });
        }
    }
    public void ApplyDiscoveryFilter()
    {
        FilteredDiscoverableGuilds.Clear();
        var category = SelectedDiscoveryCategory;
        var query = DiscoverySearchQuery?.Trim();
        foreach (var g in DiscoverableGuilds)
        {
            if (!string.IsNullOrEmpty(category) && category != "Tous" && !string.Equals(g.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!string.IsNullOrEmpty(query))
            {
                var matchesName = g.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchesDesc = g.Description != null && g.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchesKeywords = g.Keywords != null && g.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (!matchesName && !matchesDesc && !matchesKeywords)
                {
                    continue;
                }
            }
            FilteredDiscoverableGuilds.Add(g);
        }
        OnPropertyChanged(nameof(HasFilteredDiscoverableGuilds));
        OnPropertyChanged(nameof(DiscoveryResultsCountText));
    }
    public async Task JoinDiscoverableGuildAsync(DiscordDiscoverableGuild guild)
    {
        var localGuild = Guilds.FirstOrDefault(g => g.Id == guild.Id);
        if (localGuild != null)
        {
            IsDiscoveryOpen = false;
            await SelectGuildAsync(localGuild);
            return;
        }
        var inviteCode = !string.IsNullOrWhiteSpace(guild.VanityUrlCode) ? guild.VanityUrlCode : guild.Id;
        try
        {
            await JoinServerByInviteAsync(inviteCode);
            IsDiscoveryOpen = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JoinDiscoverableGuildAsync] Error: {ex.Message}");
        }
    }
    public async Task SendMessageAsync(string content)
    {
        if (_selectedChannel is null || string.IsNullOrWhiteSpace(content)) return;
        try
        {
            if (EditingMessage is not null)
            {
                var targetId = EditingMessage.Id;
                CancelComposerAction();
                var updated = await _client.Rest.EditMessageAsync(_selectedChannel.Id, targetId, content);
                OnMessageUpdated(updated);
                return;
            }
            // Auto-conversion en message.txt si > 4000 caractères (fonctionnalité SakuraCord)
            if (App.Settings.Current.ConvertLongMessagesToFile && content.Length > 4000)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                CancelComposerAction();
                await _client.Rest.SendBytesAsync(_selectedChannel.Id, bytes, "message.txt", "Message long envoyé sous forme de fichier :");
                return;
            }
            var replyId = ReplyingToMessage?.Id;
            CancelComposerAction();
            await _client.Rest.SendMessageAsync(_selectedChannel.Id, content, replyToMessageId: replyId);
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] SendMessageAsync failed: {ex}\n\n");
        }
    }
    public async Task SendStickerAsync(string stickerId)
    {
        if (_selectedChannel is null || string.IsNullOrWhiteSpace(stickerId)) return;
        try
        {
            var replyId = ReplyingToMessage?.Id;
            CancelComposerAction();
            var sent = await _client.Rest.SendStickerAsync(_selectedChannel.Id, stickerId, replyToMessageId: replyId);
            if (sent != null && Messages.All(m => m.Id != sent.Id))
            {
                sent.IsOwnMessage = true;
                Messages.Add(sent);
            }
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] SendStickerAsync failed: {ex}\n\n");
        }
    }
    public async Task SendFileAsync(string filePath)
    {
        if (_selectedChannel is null || string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            await _client.Rest.SendFileAsync(_selectedChannel.Id, filePath, anonymizeFilename: App.Settings.Current.AnonymizeUploads);
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] SendFileAsync failed: {ex}\n\n");
        }
    }
    public async Task SendBytesAsync(byte[] bytes, string fileName, string? caption = null)
    {
        if (_selectedChannel is null || bytes.Length == 0) return;
        try
        {
            var finalName = App.Settings.Current.AnonymizeUploads
                ? $"clipboard_{Guid.NewGuid().ToString("N")[..8]}{System.IO.Path.GetExtension(fileName)}"
                : fileName;
            await _client.Rest.SendBytesAsync(_selectedChannel.Id, bytes, finalName, caption);
        }
        catch (Exception ex)
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log"),
                $"[{DateTime.Now}] SendBytesAsync failed: {ex}\n\n");
        }
    }
    public async Task LoadPinnedMessagesAsync()
    {
        if (_selectedChannel is null) return;
        PinnedMessages.Clear();
        OnPropertyChanged(nameof(HasPinnedMessages));
        OnPropertyChanged(nameof(PinnedMessagesCountText));
        try
        {
            var pins = await _client.Rest.GetPinnedMessagesAsync(_selectedChannel.Id);
            foreach (var pin in pins) PinnedMessages.Add(pin);
        }
        catch { }
        finally
        {
            OnPropertyChanged(nameof(HasPinnedMessages));
            OnPropertyChanged(nameof(PinnedMessagesCountText));
        }
    }
    public async Task TogglePinMessageAsync(DiscordMessage message)
    {
        if (_selectedChannel is null) return;
        try
        {
            if (message.Pinned)
            {
                await _client.Rest.UnpinMessageAsync(_selectedChannel.Id, message.Id);
                message.Pinned = false;
                var existing = PinnedMessages.FirstOrDefault(p => p.Id == message.Id);
                if (existing != null) PinnedMessages.Remove(existing);
            }
            else
            {
                await _client.Rest.PinMessageAsync(_selectedChannel.Id, message.Id);
                message.Pinned = true;
                if (!PinnedMessages.Any(p => p.Id == message.Id)) PinnedMessages.Insert(0, message);
            }
            OnPropertyChanged(nameof(HasPinnedMessages));
            OnPropertyChanged(nameof(PinnedMessagesCountText));
        }
        catch { }
    }
    private System.Threading.CancellationTokenSource? _searchCts;
    public async Task SearchCurrentChannelAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var ct = _searchCts.Token;
        ChannelSearchResults.Clear();
        OnPropertyChanged(nameof(HasChannelSearchResults));
        OnPropertyChanged(nameof(ChannelSearchResultsCountText));
        if (string.IsNullOrWhiteSpace(query))
        {
            IsSearchingChannel = false;
            return;
        }
        IsSearchingChannel = true;
        try
        {
            // 1. Instant local search from loaded messages
            var localMatches = Messages
                .Where(m => (!string.IsNullOrEmpty(m.Content) && m.Content.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                            (m.Author?.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
            foreach (var msg in localMatches)
            {
                if (!ChannelSearchResults.Any(r => r.Id == msg.Id))
                    ChannelSearchResults.Add(msg);
            }
            // 2. Query REST search endpoint if channel is selected
            if (_selectedChannel != null)
            {
                var remoteMatches = await _client.Rest.SearchChannelMessagesAsync(_selectedChannel.Id, query, ct);
                if (!ct.IsCancellationRequested)
                {
                    foreach (var msg in remoteMatches)
                    {
                        if (!ChannelSearchResults.Any(r => r.Id == msg.Id))
                            ChannelSearchResults.Add(msg);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            IsSearchingChannel = false;
            OnPropertyChanged(nameof(HasChannelSearchResults));
            OnPropertyChanged(nameof(ChannelSearchResultsCountText));
        }
    }
    public async Task LoadMentionsAsync()
    {
        RecentMentions.Clear();
        try
        {
            var mentions = await _client.Rest.GetMentionsAsync(30);
            foreach (var m in mentions)
            {
                if (!string.IsNullOrEmpty(m.GuildId))
                {
                    var g = Guilds.FirstOrDefault(x => x.Id == m.GuildId);
                    if (g != null)
                    {
                        m.GuildName = g.Name;
                        m.GuildIconUrl = g.IconUrl;
                        var ch = g.Channels?.FirstOrDefault(c => c.Id == m.ChannelId);
                        if (ch != null) m.ChannelName = ch.DisplayName;
                    }
                }
                else
                {
                    var dm = PrivateChannels.FirstOrDefault(c => c.Id == m.ChannelId);
                    if (dm != null)
                    {
                        m.GuildName = "Message privé";
                        m.ChannelName = dm.DisplayName;
                        m.GuildIconUrl = dm.IconUrl;
                    }
                }
                RecentMentions.Add(m);
            }
        }
        catch { }
        finally
        {
            OnPropertyChanged(nameof(HasMentionsInInbox));
            OnPropertyChanged(nameof(HasNoMentionsInInbox));
        }
    }
    public void RefreshUnreadChannels()
    {
        UnreadChannels.Clear();
        foreach (var g in Guilds)
        {
            if (g.Channels == null) continue;
            foreach (var ch in g.Channels)
            {
                if (ch.HasUnread || ch.MentionCount > 0)
                {
                    ch.GuildName = g.Name;
                    ch.GuildIconUrl = g.IconUrl;
                    UnreadChannels.Add(ch);
                }
            }
        }
        foreach (var dm in PrivateChannels)
        {
            if (dm.HasUnread || dm.MentionCount > 0)
            {
                dm.GuildName = "Message privé";
                dm.GuildIconUrl = dm.IconUrl;
                UnreadChannels.Add(dm);
            }
        }
        OnPropertyChanged(nameof(HasUnreadChannels));
        OnPropertyChanged(nameof(UnreadChannelsCountText));
    }
    public void MarkAllAsRead()
    {
        foreach (var g in Guilds)
        {
            g.MentionCount = 0;
            g.HasUnread = false;
            if (g.Channels == null) continue;
            foreach (var ch in g.Channels)
            {
                ch.MentionCount = 0;
                ch.HasUnread = false;
            }
        }
        foreach (var dm in PrivateChannels)
        {
            dm.MentionCount = 0;
            dm.HasUnread = false;
        }
        UnreadChannels.Clear();
        OnPropertyChanged(nameof(HasUnreadChannels));
        OnPropertyChanged(nameof(UnreadChannelsCountText));
        OnPropertyChanged(nameof(TotalDmMentionCount));
    }
    public void ToggleBookmark(DiscordMessage msg)
    {
        var existing = Bookmarks.FirstOrDefault(b => b.Id == msg.Id);
        if (existing != null)
        {
            Bookmarks.Remove(existing);
        }
        else
        {
            Bookmarks.Insert(0, msg);
        }
        SaveBookmarks();
        OnPropertyChanged(nameof(HasBookmarks));
        OnPropertyChanged(nameof(BookmarksCountText));
    }
    public void RemoveBookmark(DiscordMessage msg)
    {
        var existing = Bookmarks.FirstOrDefault(b => b.Id == msg.Id);
        if (existing != null)
        {
            Bookmarks.Remove(existing);
            SaveBookmarks();
            OnPropertyChanged(nameof(HasBookmarks));
            OnPropertyChanged(nameof(BookmarksCountText));
        }
    }
    private void SaveBookmarks()
    {
        try
        {
            var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Diswin");
            System.IO.Directory.CreateDirectory(folder);
            var file = System.IO.Path.Combine(folder, "bookmarks.json");
            var json = System.Text.Json.JsonSerializer.Serialize(Bookmarks);
            System.IO.File.WriteAllText(file, json);
        }
        catch { }
    }
    public void LoadBookmarks()
    {
        try
        {
            var file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Diswin", "bookmarks.json");
            if (System.IO.File.Exists(file))
            {
                var json = System.IO.File.ReadAllText(file);
                var items = System.Text.Json.JsonSerializer.Deserialize<List<DiscordMessage>>(json);
                if (items != null)
                {
                    Bookmarks.Clear();
                    foreach (var it in items) Bookmarks.Add(it);
                    OnPropertyChanged(nameof(HasBookmarks));
                    OnPropertyChanged(nameof(BookmarksCountText));
                }
            }
        }
        catch { }
    }
    public async Task AckMessageAsync(string messageId, string channelId)
    {
        try
        {
            await _client.Rest.AckMessageAsync(channelId, messageId);
        }
        catch { }
    }
    public async Task<DiscordGuild> CreateGuildAsync(
        string name,
        string? iconBase64 = null,
        List<(string Name, DiscordChannelType Type)>? templateChannels = null)
    {
        var channelsPayload = templateChannels?.Select(c => new Dictionary<string, object>
        {
            ["name"] = c.Name,
            ["type"] = (int)c.Type
        }).ToList();
        var guild = await _client.CreateGuildAsync(name, iconBase64, channelsPayload);
        await SelectGuildAsync(guild);
        // Ensure template channels are created if guild only has default channels
        if (templateChannels != null && templateChannels.Count > 0)
        {
            foreach (var ch in templateChannels)
            {
                try
                {
                    if (Channels != null && Channels.Any(c => c.Name.Equals(ch.Name, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    await CreateChannelAsync(ch.Name, ch.Type);
                }
                catch { }
            }
        }
        return guild;
    }
    public async Task<DiscordGuild?> JoinServerByInviteAsync(string inviteCodeOrUrl)
    {
        var joinedGuild = await _client.JoinInviteAsync(inviteCodeOrUrl);
        if (joinedGuild != null)
        {
            var match = Guilds.FirstOrDefault(g => g.Id == joinedGuild.Id);
            if (match != null)
            {
                await SelectGuildAsync(match);
                return match;
            }
        }
        else if (Guilds.Count > 0)
        {
            // If API didn't return full guild object, select the latest guild if one was added
            var last = Guilds.LastOrDefault();
            if (last != null)
            {
                await SelectGuildAsync(last);
                return last;
            }
        }
        return joinedGuild;
    }
    public async Task LeaveGuildAsync(DiscordGuild guild)
    {
        await _client.Rest.LeaveGuildAsync(guild.Id);
        Guilds.Remove(guild);
        if (_selectedGuild?.Id == guild.Id)
        {
            ShowFriendsView();
        }
    }
    public async Task LeaveSelectedGuildAsync()
    {
        if (_selectedGuild == null) return;
        await LeaveGuildAsync(_selectedGuild);
    }
    public async Task CreateChannelAsync(string name, DiscordChannelType type = DiscordChannelType.GuildText, string? topic = null, string? parentId = null)
    {
        if (_selectedGuild == null) return;
        var newChannel = await _client.Rest.CreateGuildChannelAsync(_selectedGuild.Id, name, type, topic, parentId);
        newChannel.GuildId = _selectedGuild.Id;
        newChannel.ParentId = parentId;
        if (type == DiscordChannelType.GuildCategory)
        {
            Channels.Add(newChannel);
            FilteredChannels.Add(newChannel);
        }
        else if (!string.IsNullOrEmpty(parentId))
        {
            var cat = Channels.FirstOrDefault(c => c.Id == parentId);
            var lastChild = Channels.LastOrDefault(c => c.ParentId == parentId);
            var target = lastChild ?? cat;
            if (target != null)
            {
                int idx = Channels.IndexOf(target);
                Channels.Insert(idx + 1, newChannel);
                int fIdx = FilteredChannels.IndexOf(target);
                if (fIdx >= 0)
                {
                    FilteredChannels.Insert(fIdx + 1, newChannel);
                }
                else if (cat != null && !cat.IsCollapsed)
                {
                    FilteredChannels.Add(newChannel);
                }
            }
            else
            {
                Channels.Add(newChannel);
                FilteredChannels.Add(newChannel);
            }
        }
        else
        {
            var firstCat = Channels.FirstOrDefault(c => c.IsCategory);
            if (firstCat != null)
            {
                int idx = Channels.IndexOf(firstCat);
                Channels.Insert(idx, newChannel);
                int fIdx = FilteredChannels.IndexOf(firstCat);
                if (fIdx >= 0) FilteredChannels.Insert(fIdx, newChannel);
                else FilteredChannels.Add(newChannel);
            }
            else
            {
                Channels.Add(newChannel);
                FilteredChannels.Add(newChannel);
            }
        }
        if (type != DiscordChannelType.GuildCategory)
        {
            await SelectChannelAsync(newChannel);
        }
    }
    public async Task ModifyChannelAsync(string channelId, string newName, string? newTopic)
    {
        var updated = await _client.Rest.ModifyChannelAsync(channelId, newName, newTopic);
        var existing = Channels.FirstOrDefault(c => c.Id == channelId);
        if (existing != null)
        {
            existing.Name = updated.Name;
            existing.Topic = updated.Topic;
            OnPropertyChanged(nameof(FilteredChannels));
        }
        if (_selectedChannel?.Id == channelId)
        {
            SelectedChannelName = updated.DisplayName;
            SelectedChannelTopic = updated.Topic ?? string.Empty;
        }
    }
    public async Task DeleteChannelAsync(string channelId)
    {
        var ok = await _client.Rest.DeleteChannelAsync(channelId);
        if (ok)
        {
            var existing = Channels.FirstOrDefault(c => c.Id == channelId);
            if (existing != null)
            {
                Channels.Remove(existing);
                FilteredChannels.Remove(existing);
            }
            if (_selectedChannel?.Id == channelId)
            {
                var next = FilteredChannels.FirstOrDefault(c => c.IsTextLike || c.IsVoiceChannel);
                if (next != null)
                {
                    await SelectChannelAsync(next);
                }
                else
                {
                    ClearActiveChannel();
                }
            }
        }
    }
    public async Task<string> CreateInviteForCurrentChannelAsync()
    {
        if (_selectedChannel is null) return string.Empty;
        return await _client.Rest.CreateChannelInviteAsync(_selectedChannel.Id);
    }
    public async Task SendFriendRequestAsync(string username)
    {
        await _client.Rest.SendFriendRequestAsync(username);
        var updated = await _client.Rest.GetRelationshipsAsync();
        _client.Friends.Clear();
        foreach (var r in updated) _client.Friends.Add(r);
        RefreshFilteredFriends();
    }
    public async Task AcceptFriendRequestAsync(DiscordRelationship rel)
    {
        await _client.Rest.PutRelationshipAsync(rel.User.Id, 1);
        rel.Type = DiscordRelationshipType.Friend;
        RefreshFilteredFriends();
    }
    public async Task RemoveOrBlockRelationshipAsync(DiscordRelationship rel, bool block = false)
    {
        if (block)
        {
            await _client.Rest.PutRelationshipAsync(rel.User.Id, 2);
            rel.Type = DiscordRelationshipType.Blocked;
        }
        else
        {
            await _client.Rest.RemoveRelationshipAsync(rel.User.Id);
            _client.Friends.Remove(rel);
        }
        RefreshFilteredFriends();
    }
    public async Task OpenOrSendDirectMessageAsync(DiscordUser targetUser, string? initialMessage = null)
    {
        try
        {
            var existingDm = PrivateChannels.FirstOrDefault(c => c.Recipients?.Any(u => u.Id == targetUser.Id) == true);
            if (existingDm is null)
            {
                existingDm = await _client.Rest.CreateDmAsync(targetUser.Id);
                if (existingDm is not null && PrivateChannels.All(c => c.Id != existingDm.Id))
                {
                    PrivateChannels.Insert(0, existingDm);
                }
            }
            if (existingDm is not null)
            {
                await SelectChannelAsync(existingDm);
                if (!string.IsNullOrWhiteSpace(initialMessage))
                {
                    await _client.Rest.SendMessageAsync(existingDm.Id, initialMessage);
                }
            }
        }
        catch { }
    }
    public async Task AddReactionAsync(string messageId, string emoji)
    {
        if (_selectedChannel is null) return;
        try
        {
            await _client.Rest.AddReactionAsync(_selectedChannel.Id, messageId, emoji);
        }
        catch (Exception) { }
    }
    public async Task RemoveReactionAsync(string messageId, string emoji)
    {
        if (_selectedChannel is null) return;
        try
        {
            await _client.Rest.RemoveReactionAsync(_selectedChannel.Id, messageId, emoji);
        }
        catch (Exception) { }
    }
    public async Task ToggleReactionAsync(string messageId, DiscordReaction reaction)
    {
        if (_selectedChannel is null) return;
        try
        {
            var code = reaction.Emoji.ReactionCode;
            if (reaction.Me)
            {
                await _client.Rest.RemoveReactionAsync(_selectedChannel.Id, messageId, code);
            }
            else
            {
                await _client.Rest.AddReactionAsync(_selectedChannel.Id, messageId, code);
            }
        }
        catch (Exception) { }
    }
    public async Task DeleteMessageAsync(string messageId)
    {
        if (_selectedChannel is null) return;
        try
        {
            await _client.Rest.DeleteMessageAsync(_selectedChannel.Id, messageId);
        }
        catch (Exception) { }
    }
    private void OnMessageReceived(DiscordMessage message)
    {
        if (ActiveSplitThread != null && message.ChannelId == ActiveSplitThread.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                _client.ApplyCachedPresenceToUser(message.Author);
                ProcessMessageForGrouping(message, ActiveThreadMessages.LastOrDefault());
                ActiveThreadMessages.Add(message);
            });
        }
        if (message.ChannelId == _selectedChannel?.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                _client.ApplyCachedPresenceToUser(message.Author);
                ProcessMessageForGrouping(message, Messages.LastOrDefault());
                Messages.Add(message);
                if (ChannelMembers.All(u => u.Id != message.Author.Id))
                {
                    ChannelMembers.Add(message.Author);
                }
            });
        }
    }
    private void OnMessageUpdated(DiscordMessage message)
    {
        if (ActiveSplitThread != null && message.ChannelId == ActiveSplitThread.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = ActiveThreadMessages.FirstOrDefault(m => m.Id == message.Id);
                if (existing != null)
                {
                    var idx = ActiveThreadMessages.IndexOf(existing);
                    message.ShowHeader = existing.ShowHeader;
                    message.IsOwnMessage = _client.CurrentUser != null && message.Author.Id == _client.CurrentUser.Id;
                    ActiveThreadMessages[idx] = message;
                }
            });
        }
        if (message.ChannelId == _selectedChannel?.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = Messages.FirstOrDefault(m => m.Id == message.Id);
                if (existing != null)
                {
                    var idx = Messages.IndexOf(existing);
                    message.ShowHeader = existing.ShowHeader;
                    message.IsOwnMessage = _client.CurrentUser != null && message.Author.Id == _client.CurrentUser.Id;
                    Messages[idx] = message;
                }
            });
        }
    }
    private void OnMessageDeleted(string channelId, string messageId)
    {
        if (ActiveSplitThread != null && channelId == ActiveSplitThread.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = ActiveThreadMessages.FirstOrDefault(m => m.Id == messageId);
                if (existing != null) ActiveThreadMessages.Remove(existing);
            });
        }
        if (channelId == _selectedChannel?.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = Messages.FirstOrDefault(m => m.Id == messageId);
                if (existing != null) Messages.Remove(existing);
            });
        }
    }
    private void OnReactionAdded(string channelId, string messageId, DiscordEmoji emoji)
    {
        if (ActiveSplitThread != null && channelId == ActiveSplitThread.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = ActiveThreadMessages.FirstOrDefault(m => m.Id == messageId);
                if (existing != null)
                {
                    var reaction = existing.Reactions.FirstOrDefault(r => r.Emoji.Name == emoji.Name);
                    if (reaction != null)
                    {
                        reaction.Count++;
                    }
                    else
                    {
                        existing.Reactions.Add(new DiscordReaction { Count = 1, Emoji = emoji });
                    }
                    var idx = ActiveThreadMessages.IndexOf(existing);
                    ActiveThreadMessages[idx] = existing;
                }
            });
        }
        if (channelId == _selectedChannel?.Id)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                var existing = Messages.FirstOrDefault(m => m.Id == messageId);
                if (existing != null)
                {
                    var reaction = existing.Reactions.FirstOrDefault(r => r.Emoji.Name == emoji.Name);
                    if (reaction != null)
                    {
                        reaction.Count++;
                    }
                    else
                    {
                        existing.Reactions.Add(new DiscordReaction { Count = 1, Emoji = emoji });
                    }
                    var idx = Messages.IndexOf(existing);
                    Messages[idx] = existing;
                }
            });
        }
    }
    private void ProcessMessageForGrouping(DiscordMessage current, DiscordMessage? previous)
    {
        current.IsOwnMessage = _client.CurrentUser != null && current.Author.Id == _client.CurrentUser.Id;
        if (_selectedGuild != null)
        {
            current.Author.UpdateDisplayRoles(_selectedGuild.Roles);
        }
        if (previous == null) return;
        if (!current.IsNormal || !previous.IsNormal) return;
        if (current.IsReply) return; // Toujours afficher l'en-tête pour une réponse
        if (current.Author.Id != previous.Author.Id) return;
        var timeDiff = current.Timestamp - previous.Timestamp;
        if (timeDiff.TotalMinutes < 7)
        {
            current.ShowHeader = false;
        }
    }
    private void OnVoiceParticipantsChanged(string channelId)
    {
        Protocol.App.RunOnUiThread(() =>
        {
            if (channelId == _selectedChannel?.Id)
            {
                RefreshVoiceParticipants(channelId);
            }
            var ch = Channels.FirstOrDefault(c => c.Id == channelId);
            if (ch != null)
            {
                SyncChannelVoiceMembers(ch);
            }
            SyncOnlineOfflineMembers();
            RefreshRecentMemberActivities();
        });
    }
    private void SyncChannelVoiceMembers(DiscordChannel channel)
    {
        var members = _client.GetVoiceChannelParticipants(channel.Id);
        channel.ConnectedVoiceMembers.Clear();
        var seen = new HashSet<string>();
        foreach (var m in members)
        {
            if (seen.Add(m.Id))
            {
                if (_selectedGuild?.Roles != null && m.Roles != null)
                {
                    m.UpdateDisplayRoles(_selectedGuild.Roles);
                }
                channel.ConnectedVoiceMembers.Add(m);
            }
        }
        channel.NotifyVoiceMembersChanged();
    }
    private void RefreshVoiceParticipants(string channelId)
    {
        VoiceParticipants.Clear();
        var seen = new HashSet<string>();
        foreach (var user in _client.GetVoiceChannelParticipants(channelId))
        {
            if (seen.Add(user.Id))
            {
                if (_selectedGuild?.Roles != null && user.Roles != null)
                {
                    user.UpdateDisplayRoles(_selectedGuild.Roles);
                }
                VoiceParticipants.Add(user);
            }
        }
        if (IsVoiceConnected && _selectedChannel?.Id == channelId && _client.CurrentUser is not null && seen.Add(_client.CurrentUser.Id))
        {
            VoiceParticipants.Insert(0, _client.CurrentUser);
        }
        NotifyVoiceLayoutChanged();
    }
    public void DisconnectVoice()
    {
        bool wasConnected = IsVoiceConnected;
        _ = _client.LeaveVoiceChannelAsync();
        IsVoiceConnected = false;
        IsScreenSharing = false;
        IsCameraEnabled = false;
        ScreenShareTitle = string.Empty;
        ConnectedVoiceChannelName = string.Empty;
        VoiceParticipants.Clear();
        NotifyVoiceLayoutChanged();
        NotifyScreenShareAndCameraChanged();
        if (wasConnected)
        {
            AudioSfxService.Play(DiscordSfx.Disconnect);
        }
        if (IsVoiceChannelSelected)
        {
            IsVoiceChannelSelected = false;
            OnPropertyChanged(nameof(VoiceViewVisibility));
            OnPropertyChanged(nameof(TextViewVisibility));
            OnPropertyChanged(nameof(MemberListVisibility));
        }
    }
    public async Task StartOrJoinVoiceCallInCurrentChannelAsync()
    {
        if (_selectedChannel is null) return;
        bool wasConnected = IsVoiceConnected;
        IsVoiceChannelSelected = true;
        OnPropertyChanged(nameof(VoiceViewVisibility));
        OnPropertyChanged(nameof(TextViewVisibility));
        OnPropertyChanged(nameof(MemberListVisibility));
        var targetGuildId = _selectedChannel.GuildId ?? _selectedGuild?.Id;
        _selectedChannel.GuildId ??= targetGuildId;
        await _client.JoinVoiceChannelAsync(targetGuildId, _selectedChannel.Id, IsMuted, IsDeafened, IsCameraEnabled, IsScreenSharing);
        IsVoiceConnected = true;
        ConnectedVoiceChannelName = _selectedChannel.DisplayName;
        RefreshVoiceParticipants(_selectedChannel.Id);
        if (!wasConnected)
        {
            AudioSfxService.Play(DiscordSfx.Join);
        }
    }
    public async Task StartScreenShareAsync(string title, string qualityBadge, bool shareSystemAudio)
    {
        if (!IsVoiceConnected && _selectedChannel is not null)
        {
            await StartOrJoinVoiceCallInCurrentChannelAsync();
        }
        if (!IsVoiceConnected) return;
        IsScreenSharing = true;
        ScreenShareTitle = title;
        ScreenShareQualityBadge = qualityBadge;
        await _client.StartScreenShareAsync(shareSystemAudio);
        NotifyScreenShareAndCameraChanged();
    }
    public async Task StopScreenShareAsync()
    {
        if (!IsScreenSharing) return;
        IsScreenSharing = false;
        ScreenShareTitle = string.Empty;
        await _client.StopScreenShareAsync();
        NotifyScreenShareAndCameraChanged();
    }
    public async Task ToggleCameraAsync()
    {
        if (!IsVoiceConnected && _selectedChannel is not null)
        {
            await StartOrJoinVoiceCallInCurrentChannelAsync();
        }
        if (!IsVoiceConnected) return;
        IsCameraEnabled = !IsCameraEnabled;
        await _client.ToggleVideoAsync(IsCameraEnabled);
        NotifyScreenShareAndCameraChanged();
    }
    private void NotifyScreenShareAndCameraChanged()
    {
        OnPropertyChanged(nameof(ScreenShareStageVisibility));
        OnPropertyChanged(nameof(ScreenShareOpacity));
        OnPropertyChanged(nameof(ScreenShareIconBrush));
        OnPropertyChanged(nameof(ScreenShareTooltipText));
        OnPropertyChanged(nameof(CameraOpacity));
        OnPropertyChanged(nameof(CameraIconBrush));
        OnPropertyChanged(nameof(CameraTooltipText));
    }
    private void OnVoiceChannelEffectReceived(DiscordSoundboardEffect effect)
    {
        if (effect != null && !string.IsNullOrEmpty(effect.SoundId))
        {
            var currentVcId = _client.CurrentVoiceChannelId;
            if (!string.IsNullOrEmpty(currentVcId) && currentVcId == effect.ChannelId)
            {
                _ = SoundboardPlayerService.PlaySoundAsync(effect.SoundId, effect.SoundVolume);
            }
        }
    }
    private string _soundboardSearchQuery = string.Empty;
    public async Task LoadSoundboardSoundsAsync()
    {
        try
        {
            SoundboardGroups.Clear();
            // 1. Sons personnalisés du serveur actuel (si dans un serveur)
            if (_selectedGuild != null && !string.IsNullOrEmpty(_selectedGuild.Id))
            {
                var guildSounds = await _client.Rest.GetGuildSoundboardSoundsAsync(_selectedGuild.Id);
                if (guildSounds.Count > 0)
                {
                    var guildGroup = new DiscordSoundboardGroup
                    {
                        Header = _selectedGuild.Name,
                        IconGlyph = "\uE74F"
                    };
                    foreach (var s in guildSounds)
                    {
                        guildGroup.Sounds.Add(s);
                    }
                    guildGroup.NotifyChanged();
                    SoundboardGroups.Add(guildGroup);
                }
            }
            // 2. Sons officiels par défaut de Discord
            var defaultSounds = await _client.Rest.GetDefaultSoundboardSoundsAsync();
            var discordGroup = new DiscordSoundboardGroup
            {
                Header = "Discord",
                IconGlyph = "\uE803"
            };
            foreach (var s in defaultSounds)
            {
                discordGroup.Sounds.Add(s);
            }
            discordGroup.NotifyChanged();
            SoundboardGroups.Add(discordGroup);
            FilterSoundboardSounds(_soundboardSearchQuery);
        }
        catch { }
    }
    public void FilterSoundboardSounds(string query)
    {
        _soundboardSearchQuery = query?.Trim() ?? string.Empty;
        FilteredSoundboardGroups.Clear();
        if (string.IsNullOrEmpty(_soundboardSearchQuery))
        {
            foreach (var g in SoundboardGroups) FilteredSoundboardGroups.Add(g);
        }
        else
        {
            foreach (var g in SoundboardGroups)
            {
                var matchedSounds = g.Sounds
                    .Where(s => s.Name.Contains(_soundboardSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                (s.EmojiName != null && s.EmojiName.Contains(_soundboardSearchQuery, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (matchedSounds.Count > 0)
                {
                    var filteredGroup = new DiscordSoundboardGroup
                    {
                        Header = g.Header,
                        IconGlyph = g.IconGlyph
                    };
                    foreach (var ms in matchedSounds) filteredGroup.Sounds.Add(ms);
                    filteredGroup.NotifyChanged();
                    FilteredSoundboardGroups.Add(filteredGroup);
                }
            }
        }
        OnPropertyChanged(nameof(HasSoundboardGroups));
    }
    public async Task PlaySoundboardSoundAsync(DiscordSoundboardSound sound, bool previewOnly = false)
    {
        if (sound == null) return;
        try
        {
            sound.IsPlaying = true;
            _ = SoundboardPlayerService.PlaySoundAsync(sound.SoundId, sound.Volume);
            var voiceChannelId = _client.CurrentVoiceChannelId;
            if (!previewOnly && !string.IsNullOrEmpty(voiceChannelId))
            {
                await _client.Rest.SendVoiceChannelEffectAsync(
                    voiceChannelId,
                    sound.SoundId,
                    sound.GuildId,
                    sound.EmojiName);
            }
        }
        catch { }
        finally
        {
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                Protocol.App.RunOnUiThread(() => sound.IsPlaying = false);
            });
        }
    }
    public async Task CreatePollInCurrentChannelAsync(string question, List<string> answers, int durationHours, bool allowMultiselect)
    {
        if (_selectedChannel is null) return;
        await _client.Rest.CreatePollMessageAsync(_selectedChannel.Id, question, answers, durationHours, allowMultiselect);
    }
    private DateTimeOffset _lastTypingSent = DateTimeOffset.MinValue;
    public void NotifyTypingInCurrentChannel()
    {
        if (_selectedChannel is null) return;
        var now = DateTimeOffset.UtcNow;
        if ((now - _lastTypingSent).TotalSeconds < 8) return;
        _lastTypingSent = now;
        _ = _client.Rest.TriggerTypingAsync(_selectedChannel.Id);
    }
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        OnPropertyChanged(nameof(MuteOpacity));
        AudioSfxService.Play(IsMuted ? DiscordSfx.Mute : DiscordSfx.Unmute);
        if (_selectedChannel is not null && IsVoiceConnected)
        {
            var targetGuildId = _selectedChannel.GuildId ?? _selectedGuild?.Id;
            _ = _client.JoinVoiceChannelAsync(targetGuildId, _selectedChannel.Id, IsMuted, IsDeafened, IsCameraEnabled, IsScreenSharing);
        }
    }
    public void ToggleDeafen()
    {
        IsDeafened = !IsDeafened;
        if (IsDeafened) IsMuted = true;
        OnPropertyChanged(nameof(DeafenOpacity));
        OnPropertyChanged(nameof(MuteOpacity));
        AudioSfxService.Play(IsDeafened ? DiscordSfx.Deafen : DiscordSfx.Undeafen);
        if (_selectedChannel is not null && IsVoiceConnected)
        {
            var targetGuildId = _selectedChannel.GuildId ?? _selectedGuild?.Id;
            _ = _client.JoinVoiceChannelAsync(targetGuildId, _selectedChannel.Id, IsMuted, IsDeafened, IsCameraEnabled, IsScreenSharing);
        }
    }
    public void ToggleKrisp()
    {
        SetKrispEnabled(!IsKrispEnabled);
    }
    public void SetKrispEnabled(bool enabled)
    {
        IsKrispEnabled = enabled;
        _client.IsKrispEnabled = enabled;
        App.Settings.Current.EnableKrispNoiseSuppression = enabled;
        App.Settings.Save();
        OnPropertyChanged(nameof(KrispOpacity));
        OnPropertyChanged(nameof(KrispIconBrush));
        OnPropertyChanged(nameof(KrispTooltipText));
    }
    public void SetStatus(string status)
    {
        CurrentStatus = status;
        OnPropertyChanged(nameof(StatusDotBrush));
        OnPropertyChanged(nameof(CurrentStatusDisplayName));
        OnPropertyChanged(nameof(CurrentUserSubStatusText));
        _ = _client.Rest.UpdateCustomStatusAsync(customStatusText: null, status: status);
        _ = _client.BroadcastCurrentPresenceAsync(status);
    }
    public async Task SetCustomStatusAsync(string? text, string? emojiName = null, string? expiryOption = "today", string? status = null)
    {
        DateTimeOffset? expiresAt = null;
        if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(emojiName))
        {
            expiresAt = expiryOption switch
            {
                "30m" => DateTimeOffset.UtcNow.AddMinutes(30),
                "1h" => DateTimeOffset.UtcNow.AddHours(1),
                "4h" => DateTimeOffset.UtcNow.AddHours(4),
                "today" => DateTimeOffset.UtcNow.Date.AddDays(1),
                _ => null
            };
        }
        string effectiveStatus = !string.IsNullOrWhiteSpace(status) ? status : CurrentStatus;
        await _client.Rest.UpdateCustomStatusAsync(
            customStatusText: text,
            status: effectiveStatus,
            emojiName: emojiName,
            expiresAt: expiresAt);
        if (_client.CurrentUser != null)
        {
            _client.CurrentUser.Activities.RemoveAll(a => a.Type == 4);
            if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(emojiName))
            {
                var newActivity = new DiscordActivity
                {
                    Name = "Custom Status",
                    Type = 4,
                    State = text,
                    Emoji = !string.IsNullOrWhiteSpace(emojiName) ? new DiscordActivityEmoji { Name = emojiName } : null
                };
                _client.CurrentUser.Activities.Add(newActivity);
            }
            _client.CurrentUser.NotifyActivityTimesChanged();
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            CurrentStatus = status;
        }
        await _client.BroadcastCurrentPresenceAsync(effectiveStatus);
        OnPropertyChanged(nameof(StatusDotBrush));
        OnPropertyChanged(nameof(CurrentStatusDisplayName));
        OnPropertyChanged(nameof(CurrentUserSubStatusText));
        OnPropertyChanged(nameof(CurrentUserCustomStatusText));
        OnPropertyChanged(nameof(CurrentUserCustomStatusEmoji));
        OnPropertyChanged(nameof(HasCustomStatus));
        OnPropertyChanged(nameof(CustomStatusButtonText));
        OnPropertyChanged(nameof(CurrentUserCustomStatusSubtitle));
    }
    public async Task ClearCustomStatusAsync()
    {
        await SetCustomStatusAsync(null, null, "never");
    }
    public async Task SetCustomRichPresenceAsync(DiscordActivity? customActivity)
    {
        await _client.SetCustomRichPresenceAsync(customActivity, CurrentStatus);
        NotifyCurrentUserRichPresenceChanged();
    }
    public void ApplySearchFilter(string query)
    {
        SearchFilterText = query;
        FilteredChannels.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var ch in Channels) FilteredChannels.Add(ch);
            return;
        }
        var lower = query.ToLowerInvariant();
        foreach (var ch in Channels.Where(c => c.DisplayName.ToLowerInvariant().Contains(lower)))
        {
            FilteredChannels.Add(ch);
        }
    }
    private DiscordClanCardViewModel? _activeClanCard;
    public DiscordClanCardViewModel? ActiveClanCard
    {
        get => _activeClanCard;
        set
        {
            if (_activeClanCard != value)
            {
                _activeClanCard = value;
                OnPropertyChanged(nameof(ActiveClanCard));
                OnPropertyChanged(nameof(HasActiveClanCard));
            }
        }
    }
    public bool HasActiveClanCard => ActiveClanCard != null;
    public async Task<DiscordClanCardViewModel> LoadClanCardAsync(DiscordClan clan)
    {
        var card = new DiscordClanCardViewModel
        {
            GuildId = clan.IdentityGuildId ?? string.Empty,
            Tag = clan.Tag ?? string.Empty,
            BadgeUrl = clan.BadgeUrl,
            IconUrl = clan.BadgeUrl,
            IsLoading = true
        };
        card.EstablishedText = DiscordClanCardViewModel.ComputeEstablishedText(card.GuildId);
        ActiveClanCard = card;
        // 1. Vérifier si la guilde est déjà dans les guildes locales
        var localGuild = Guilds.FirstOrDefault(g => g.Id == card.GuildId);
        if (localGuild != null)
        {
            card.UpdateFromGuild(localGuild, clan.Tag, clan.BadgeUrl);
            card.IsLoading = false;
        }
        // 2. Tenter de charger les données complètes (preview / profil / stats / jeux) depuis l'API
        if (!string.IsNullOrWhiteSpace(card.GuildId))
        {
            try
            {
                var rawJson = await _client.GetGuildPreviewOrProfileAsync(card.GuildId);
                if (rawJson.HasValue)
                {
                    card.UpdateFromPreview(rawJson.Value, clan.Tag, clan.BadgeUrl);
                    if (localGuild != null) card.IsAlreadyJoined = true;
                }
            }
            catch { }
        }
        card.IsLoading = false;
        return card;
    }
    public async Task JoinOrAccessClanGuildAsync(DiscordClanCardViewModel card)
    {
        if (card.IsAlreadyJoined)
        {
            var guild = Guilds.FirstOrDefault(g => g.Id == card.GuildId);
            if (guild != null)
            {
                await SelectGuildAsync(guild);
            }
        }
        else if (!string.IsNullOrWhiteSpace(card.VanityUrlCode))
        {
            try
            {
                await JoinServerByInviteAsync(card.VanityUrlCode);
            }
            catch { }
        }
    }
    public void UpdateAutocomplete(string text, int cursor)
    {
        if (string.IsNullOrEmpty(text) || cursor <= 0 || cursor > text.Length)
        {
            CloseAutocomplete();
            return;
        }
        // Find the word being typed backwards from cursor
        int start = cursor - 1;
        while (start >= 0 && !char.IsWhiteSpace(text[start]))
        {
            start--;
        }
        start++; // start of the word
        if (start >= cursor)
        {
            CloseAutocomplete();
            return;
        }
        string word = text.Substring(start, cursor - start);
        if (word.StartsWith("@"))
        {
            string query = word.Substring(1).Trim().ToLowerInvariant();
            var matches = new List<AutocompleteItemViewModel>();
            // 1. @everyone and @here if query matches
            if ("everyone".StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new AutocompleteItemViewModel
                {
                    ItemType = AutocompleteItemType.SpecialMention,
                    Title = "@everyone",
                    Subtitle = "Notifie tous les membres du serveur",
                    RoleColorHex = "#FEE75C",
                    TextToInsert = "@everyone"
                });
            }
            if ("here".StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(new AutocompleteItemViewModel
                {
                    ItemType = AutocompleteItemType.SpecialMention,
                    Title = "@here",
                    Subtitle = "Notifie les membres en ligne",
                    RoleColorHex = "#FEE75C",
                    TextToInsert = "@here"
                });
            }
            // 2. Roles in current guild
            if (_selectedGuild?.Roles != null)
            {
                foreach (var role in _selectedGuild.Roles.OrderByDescending(r => r.Position))
                {
                    if (string.IsNullOrWhiteSpace(role.Name) || role.Name == "@everyone") continue;
                    if (role.Name.ToLowerInvariant().Contains(query))
                    {
                        matches.Add(new AutocompleteItemViewModel
                        {
                            ItemType = AutocompleteItemType.Role,
                            Title = $"@{role.Name}",
                            Subtitle = "Mentionner ce rôle",
                            RoleColorHex = role.ColorHex,
                            TextToInsert = $"<@&{role.Id}>"
                        });
                        if (matches.Count >= 10) break;
                    }
                }
            }
            // 3. Members in current guild / channel
            var memberSource = new List<DiscordUser>(ChannelMembers);
            if (_selectedGuild != null)
            {
                var cached = _client.GetCachedGuildMembers(_selectedGuild.Id);
                var seen = new HashSet<string>(memberSource.Select(m => m.Id));
                foreach (var m in cached)
                {
                    if (seen.Add(m.Id)) memberSource.Add(m);
                }
            }
            var filteredMembers = memberSource.Where(m =>
                (m.DisplayName != null && m.DisplayName.ToLowerInvariant().Contains(query)) ||
                (m.Username != null && m.Username.ToLowerInvariant().Contains(query)) ||
                (m.GlobalName != null && m.GlobalName.ToLowerInvariant().Contains(query))
            ).Take(15).ToList();
            foreach (var m in filteredMembers)
            {
                matches.Add(new AutocompleteItemViewModel
                {
                    ItemType = AutocompleteItemType.Member,
                    Title = m.DisplayName,
                    Subtitle = $"@{m.Username}",
                    AvatarUrl = m.AvatarUrl,
                    StatusColor = m.StatusColor,
                    TextToInsert = $"<@{m.Id}>"
                });
            }
            if (matches.Count > 0)
            {
                AutocompleteSuggestions.Clear();
                foreach (var item in matches) AutocompleteSuggestions.Add(item);
                AutocompleteCategoryTitle = "MEMBRES DU SERVEUR";
                SetAutocompleteIndex(0);
                IsAutocompleteOpen = true;
                return;
            }
        }
        else if (word.StartsWith(":") && word.Length >= 2)
        {
            string query = word.Substring(1).Trim().ToLowerInvariant();
            var matches = new List<AutocompleteItemViewModel>();
            // 1. Search in current guild emojis
            if (_selectedGuild?.Emojis != null)
            {
                foreach (var em in _selectedGuild.Emojis)
                {
                    if (string.IsNullOrEmpty(em.Name)) continue;
                    if (em.Name.ToLowerInvariant().Contains(query))
                    {
                        matches.Add(new AutocompleteItemViewModel
                        {
                            ItemType = AutocompleteItemType.Emoji,
                            Title = $":{em.Name}:",
                            Subtitle = _selectedGuild.Name ?? "Serveur",
                            IsCustomEmoji = true,
                            EmojiImageUrl = em.ImageUrl,
                            TextToInsert = em.FormattedTag
                        });
                        if (matches.Count >= 12) break;
                    }
                }
            }
            // 2. Search in all guilds emojis
            if (matches.Count < 12)
            {
                foreach (var g in Guilds)
                {
                    if (g.Id == _selectedGuild?.Id || g.Emojis == null) continue;
                    foreach (var em in g.Emojis)
                    {
                        if (string.IsNullOrEmpty(em.Name)) continue;
                        if (em.Name.ToLowerInvariant().Contains(query))
                        {
                            matches.Add(new AutocompleteItemViewModel
                            {
                                ItemType = AutocompleteItemType.Emoji,
                                Title = $":{em.Name}:",
                                Subtitle = g.Name ?? "Serveur",
                                IsCustomEmoji = true,
                                EmojiImageUrl = em.ImageUrl,
                                TextToInsert = em.FormattedTag
                            });
                            if (matches.Count >= 12) break;
                        }
                    }
                    if (matches.Count >= 12) break;
                }
            }
            // 3. Search in standard unicode emojis
            var emojiCatalog = EmojiService.Instance.BuildPickerCatalog(Guilds, _selectedGuild?.Id, query);
            if (emojiCatalog.Count > 0 && emojiCatalog[0].Emojis != null)
            {
                foreach (var em in emojiCatalog[0].Emojis.Where(e => !e.IsCustom).Take(8))
                {
                    matches.Add(new AutocompleteItemViewModel
                    {
                        ItemType = AutocompleteItemType.Emoji,
                        Title = $":{em.Name}:",
                        Subtitle = em.Category,
                        IsCustomEmoji = false,
                        EmojiUnicode = em.UnicodeChar,
                        TextToInsert = em.UnicodeChar ?? em.Name
                    });
                    if (matches.Count >= 16) break;
                }
            }
            if (matches.Count > 0)
            {
                AutocompleteSuggestions.Clear();
                foreach (var item in matches) AutocompleteSuggestions.Add(item);
                AutocompleteCategoryTitle = "ÉMOJIS";
                SetAutocompleteIndex(0);
                IsAutocompleteOpen = true;
                return;
            }
        }
        CloseAutocomplete();
    }
    public void SelectNextAutocomplete()
    {
        if (AutocompleteSuggestions.Count == 0) return;
        int next = (AutocompleteSelectedIndex + 1) % AutocompleteSuggestions.Count;
        SetAutocompleteIndex(next);
    }
    public void SelectPreviousAutocomplete()
    {
        if (AutocompleteSuggestions.Count == 0) return;
        int prev = AutocompleteSelectedIndex - 1;
        if (prev < 0) prev = AutocompleteSuggestions.Count - 1;
        SetAutocompleteIndex(prev);
    }
    private void SetAutocompleteIndex(int index)
    {
        if (index < 0 || index >= AutocompleteSuggestions.Count) return;
        for (int i = 0; i < AutocompleteSuggestions.Count; i++)
        {
            AutocompleteSuggestions[i].IsSelected = (i == index);
        }
        AutocompleteSelectedIndex = index;
    }
    public AutocompleteItemViewModel? GetSelectedAutocompleteItem()
    {
        if (AutocompleteSelectedIndex >= 0 && AutocompleteSelectedIndex < AutocompleteSuggestions.Count)
        {
            return AutocompleteSuggestions[AutocompleteSelectedIndex];
        }
        return AutocompleteSuggestions.FirstOrDefault();
    }
    public void CloseAutocomplete()
    {
        IsAutocompleteOpen = false;
        AutocompleteSelectedIndex = -1;
        AutocompleteSuggestions.Clear();
    }
    public async Task LoadTrendingGifsAsync()
    {
        IsLoadingGifs = true;
        try
        {
            var gifs = await _client.Rest.GetTrendingGifsAsync();
            GifResults.Clear();
            foreach (var g in gifs) GifResults.Add(g);
        }
        catch { }
        finally
        {
            IsLoadingGifs = false;
        }
    }
    public async Task SearchGifsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadTrendingGifsAsync();
            return;
        }
        IsLoadingGifs = true;
        try
        {
            var gifs = await _client.Rest.SearchGifsAsync(query);
            GifResults.Clear();
            foreach (var g in gifs) GifResults.Add(g);
        }
        catch { }
        finally
        {
            IsLoadingGifs = false;
        }
    }
}
