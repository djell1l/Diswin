using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordUser : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void NotifyPresenceChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(Activities));
        OnPropertyChanged(nameof(PrimaryActivity));
        OnPropertyChanged(nameof(CustomStatusActivity));
        OnPropertyChanged(nameof(HasCustomStatus));
        OnPropertyChanged(nameof(CustomStatusText));
        OnPropertyChanged(nameof(HasRichPresence));
        OnPropertyChanged(nameof(HasActivityOrCustomStatus));
        OnPropertyChanged(nameof(ActivitySummaryText));
        OnPropertyChanged(nameof(HasGameActivity));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentGameIconUrl));
        OnPropertyChanged(nameof(HasCurrentGameIcon));
        OnPropertyChanged(nameof(GameTooltipText));
        OnPropertyChanged(nameof(HasNonGameActivitySummary));
        OnPropertyChanged(nameof(IsCurrentUser));
        OnPropertyChanged(nameof(IsNotCurrentUser));
    }

    public void NotifyProfileEnriched()
    {
        OnPropertyChanged(nameof(Bio));
        OnPropertyChanged(nameof(AboutMe));
        OnPropertyChanged(nameof(Pronouns));
        OnPropertyChanged(nameof(HasPronouns));
        OnPropertyChanged(nameof(Banner));
        OnPropertyChanged(nameof(BannerUrl));
        OnPropertyChanged(nameof(HasBanner));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(ThemeColors));
        OnPropertyChanged(nameof(HasCustomThemeColors));
        OnPropertyChanged(nameof(HasEffectiveCustomTheme));
        OnPropertyChanged(nameof(BannerColorHex));
        OnPropertyChanged(nameof(ProfilePrimaryColorHex));
        OnPropertyChanged(nameof(ProfileSecondaryColorHex));
        OnPropertyChanged(nameof(ProfileBorderColorHex));
        OnPropertyChanged(nameof(ProfilePillBackgroundColorHex));
        OnPropertyChanged(nameof(ProfilePillBorderColorHex));
        OnPropertyChanged(nameof(ProfileTextForegroundColorHex));
        OnPropertyChanged(nameof(ProfileSecondaryTextColorHex));
        OnPropertyChanged(nameof(ProfileMutedTextColorHex));
        OnPropertyChanged(nameof(ProfileCardInnerBackgroundColorHex));
        OnPropertyChanged(nameof(ProfileCardInnerBorderColorHex));
        OnPropertyChanged(nameof(ProfileInputBackgroundColorHex));
        OnPropertyChanged(nameof(ProfileInputBorderColorHex));
        OnPropertyChanged(nameof(ProfileAvatarCutoutStrokeColorHex));
        OnPropertyChanged(nameof(ProfileCardBackgroundColorHex));
        OnPropertyChanged(nameof(ProfileCardBorderColorHex));
        OnPropertyChanged(nameof(MutualFriendsCount));
        OnPropertyChanged(nameof(MutualGuildsCount));
        OnPropertyChanged(nameof(HasMutualInfo));
        OnPropertyChanged(nameof(MutualSummaryText));
        OnPropertyChanged(nameof(Clan));
        OnPropertyChanged(nameof(PrimaryGuild));
        OnPropertyChanged(nameof(ActiveClan));
        OnPropertyChanged(nameof(HasClan));
        OnPropertyChanged(nameof(ClanTag));
        OnPropertyChanged(nameof(ClanBadgeUrl));
        OnPropertyChanged(nameof(Badges));
        OnPropertyChanged(nameof(HasBadges));
        OnPropertyChanged(nameof(ConnectedAccounts));
        OnPropertyChanged(nameof(HasConnectedAccounts));
        OnPropertyChanged(nameof(GameWidgets));
        OnPropertyChanged(nameof(HasGameWidgets));
        OnPropertyChanged(nameof(DisplayRoles));
        OnPropertyChanged(nameof(HasDisplayRoles));
        OnPropertyChanged(nameof(HasMutualFriends));
        OnPropertyChanged(nameof(MutualFriendsTabTitle));
        OnPropertyChanged(nameof(HasMutualGuilds));
        OnPropertyChanged(nameof(MutualServersTabTitle));
        OnPropertyChanged(nameof(HasRecentActivities));
        OnPropertyChanged(nameof(HasWishlist));
        OnPropertyChanged(nameof(WishlistTabTitle));
        OnPropertyChanged(nameof(IsFriend));
        OnPropertyChanged(nameof(FriendActionText));
        OnPropertyChanged(nameof(FriendActionGlyph));
        OnPropertyChanged(nameof(FriendStatusTooltip));
        OnPropertyChanged(nameof(IsCurrentUser));
        OnPropertyChanged(nameof(IsNotCurrentUser));
    }

    public bool HasProfileEnriched { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("discriminator")]
    public string Discriminator { get; set; } = "0";

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("bot")]
    public bool Bot { get; set; }

    [JsonPropertyName("banner")]
    public string? Banner { get; set; }

    [JsonPropertyName("accent_color")]
    public int? AccentColor { get; set; }

    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();

    private readonly ObservableCollection<DiscordRole> _displayRoles = new();
    [JsonIgnore]
    public ObservableCollection<DiscordRole> DisplayRoles => _displayRoles;

    [JsonIgnore]
    public bool HasDisplayRoles => _displayRoles.Count > 0;

    public void UpdateDisplayRoles(IEnumerable<DiscordRole>? guildRoles)
    {
        _displayRoles.Clear();
        if (guildRoles != null && Roles != null && Roles.Count > 0)
        {
            var roleMap = guildRoles.ToDictionary(r => r.Id);
            var resolved = new List<DiscordRole>();
            foreach (var rid in Roles)
            {
                if (roleMap.TryGetValue(rid, out var r))
                {
                    resolved.Add(r);
                }
            }
            foreach (var r in resolved.OrderByDescending(r => r.Position))
            {
                _displayRoles.Add(r);
            }
        }
        OnPropertyChanged(nameof(HasDisplayRoles));
    }

    private string? _currentVoiceChannelName;
    [JsonIgnore]
    public string? CurrentVoiceChannelName
    {
        get => _currentVoiceChannelName;
        set
        {
            if (_currentVoiceChannelName != value)
            {
                _currentVoiceChannelName = value;
                OnPropertyChanged(nameof(CurrentVoiceChannelName));
                OnPropertyChanged(nameof(IsInVoice));
                OnPropertyChanged(nameof(ActivitySummaryText));
            }
        }
    }

    [JsonIgnore]
    public bool IsInVoice => !string.IsNullOrWhiteSpace(_currentVoiceChannelName);

    public string QuickMessagePlaceholder => $"Message @{DisplayName}";

    public static bool ForceAccentColorOnAllProfiles { get; set; } = false;

    public static double CalculateLuminance(int rgb)
    {
        int r = (rgb >> 16) & 0xFF;
        int g = (rgb >> 8) & 0xFF;
        int b = rgb & 0xFF;
        return (0.299 * r) + (0.587 * g) + (0.114 * b);
    }

    public List<int> ThemeColors { get; set; } = new();
    public bool HasCustomThemeColors => ThemeColors.Count >= 2;
    public bool HasEffectiveCustomTheme => HasCustomThemeColors || (ForceAccentColorOnAllProfiles && AccentColor.HasValue);

    public string BannerColorHex
    {
        get
        {
            if (AccentColor is int ac) return $"#{ac:X6}";
            if (HasCustomThemeColors) return $"#{ThemeColors[0]:X6}";
            return "#232428";
        }
    }

    public string ProfilePrimaryColorHex
    {
        get
        {
            if (HasCustomThemeColors) return $"#{ThemeColors[0]:X6}";
            if (ForceAccentColorOnAllProfiles && AccentColor is int ac) return $"#{ac:X6}";
            return "#232428";
        }
    }

    public string ProfileSecondaryColorHex
    {
        get
        {
            if (HasCustomThemeColors) return $"#{ThemeColors[1]:X6}";
            if (ForceAccentColorOnAllProfiles && AccentColor is int ac) return $"#{ac:X6}";
            return "#111214";
        }
    }

    public string ProfileBorderColorHex
    {
        get
        {
            if (HasCustomThemeColors) return $"#{ThemeColors[0]:X6}";
            if (ForceAccentColorOnAllProfiles && AccentColor is int ac) return $"#{ac:X6}";
            return "#383A40";
        }
    }

    public bool IsThemeLight
    {
        get
        {
            if (!HasEffectiveCustomTheme) return false;
            if (HasCustomThemeColors)
            {
                int c0 = ThemeColors[0];
                int c1 = ThemeColors[1];
                double lum0 = CalculateLuminance(c0);
                double lum1 = CalculateLuminance(c1);
                double avg = (lum0 + lum1) / 2.0;
                return avg >= 125.0 || (Math.Max(lum0, lum1) >= 170.0 && avg >= 115.0);
            }
            if (ForceAccentColorOnAllProfiles && AccentColor is int ac)
            {
                return CalculateLuminance(ac) >= 125.0;
            }
            return false;
        }
    }

    public string ProfileTextForegroundColorHex => IsThemeLight ? "#111214" : "#FFFFFF";
    public string ProfileSecondaryTextColorHex => IsThemeLight ? "#313338" : "#DBDEE1";
    public string ProfileMutedTextColorHex => IsThemeLight ? "#5C5E66" : "#949BA4";
    public string ProfilePillBackgroundColorHex => IsThemeLight ? "#1F000000" : "#33FFFFFF";
    public string ProfilePillBorderColorHex => IsThemeLight ? "#2F000000" : "#33FFFFFF";
    public string ProfileCardInnerBackgroundColorHex => IsThemeLight ? "#14000000" : "#33000000";
    public string ProfileCardInnerBorderColorHex => IsThemeLight ? "#22000000" : "#22FFFFFF";
    public string ProfileInputBackgroundColorHex => IsThemeLight ? "#18000000" : "#33000000";
    public string ProfileInputBorderColorHex => IsThemeLight ? "#35000000" : "#22FFFFFF";
    public string ProfileAvatarCutoutStrokeColorHex => ProfilePrimaryColorHex;
    public string ProfileCardBackgroundColorHex => IsThemeLight ? "#18000000" : "#2B2D31";
    public string ProfileCardBorderColorHex => IsThemeLight ? "#22000000" : "#35373C";

    private int _mutualFriendsCount;
    public int MutualFriendsCount
    {
        get => _mutualFriendsCount;
        set
        {
            if (_mutualFriendsCount != value)
            {
                _mutualFriendsCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMutualInfo));
                OnPropertyChanged(nameof(MutualSummaryText));
                OnPropertyChanged(nameof(MutualFriendsTabTitle));
                OnPropertyChanged(nameof(HasMutualFriends));
            }
        }
    }

    private int _mutualGuildsCount;
    public int MutualGuildsCount
    {
        get => _mutualGuildsCount;
        set
        {
            if (_mutualGuildsCount != value)
            {
                _mutualGuildsCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMutualInfo));
                OnPropertyChanged(nameof(MutualSummaryText));
                OnPropertyChanged(nameof(MutualServersTabTitle));
                OnPropertyChanged(nameof(HasMutualGuilds));
            }
        }
    }

    public bool HasMutualInfo => MutualFriendsCount > 0 || MutualGuildsCount > 0;
    public string MutualSummaryText
    {
        get
        {
            var parts = new List<string>();
            if (MutualFriendsCount > 0) parts.Add($"{MutualFriendsCount} Mutual Friend{(MutualFriendsCount > 1 ? "s" : "")}");
            if (MutualGuildsCount > 0) parts.Add($"{MutualGuildsCount} Mutual Server{(MutualGuildsCount > 1 ? "s" : "")}");
            return string.Join(" • ", parts);
        }
    }

    public string MutualFriendsTabTitle => $"{MutualFriendsCount} Mutual Friend{(MutualFriendsCount > 1 ? "s" : "")}";
    public string MutualServersTabTitle => $"{MutualGuildsCount} Mutual Server{(MutualGuildsCount > 1 ? "s" : "")}";

    private List<DiscordMutualGuild> _mutualGuilds = new();
    public List<DiscordMutualGuild> MutualGuilds
    {
        get => _mutualGuilds;
        set
        {
            if (_mutualGuilds != value)
            {
                _mutualGuilds = value;
                if (_mutualGuilds.Count > 0 && _mutualGuildsCount == 0)
                {
                    _mutualGuildsCount = _mutualGuilds.Count;
                    OnPropertyChanged(nameof(MutualGuildsCount));
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMutualGuilds));
                OnPropertyChanged(nameof(MutualServersTabTitle));
                OnPropertyChanged(nameof(HasMutualInfo));
                OnPropertyChanged(nameof(MutualSummaryText));
            }
        }
    }
    public bool HasMutualGuilds => MutualGuilds.Count > 0 || MutualGuildsCount > 0;

    private List<DiscordUser> _mutualFriends = new();
    public List<DiscordUser> MutualFriends
    {
        get => _mutualFriends;
        set
        {
            if (_mutualFriends != value)
            {
                _mutualFriends = value;
                if (_mutualFriends.Count > 0 && _mutualFriendsCount == 0)
                {
                    _mutualFriendsCount = _mutualFriends.Count;
                    OnPropertyChanged(nameof(MutualFriendsCount));
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasMutualFriends));
                OnPropertyChanged(nameof(MutualFriendsTabTitle));
                OnPropertyChanged(nameof(HasMutualInfo));
                OnPropertyChanged(nameof(MutualSummaryText));
            }
        }
    }
    public bool HasMutualFriends => MutualFriends.Count > 0 || MutualFriendsCount > 0;

    private List<DiscordActivity> _recentActivities = new();
    public List<DiscordActivity> RecentActivities
    {
        get => _recentActivities;
        set
        {
            if (_recentActivities != value)
            {
                _recentActivities = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasRecentActivities));
            }
        }
    }
    public bool HasRecentActivities => RecentActivities.Count > 0;

    private int _wishlistCount;
    public int WishlistCount
    {
        get => _wishlistCount;
        set
        {
            if (_wishlistCount != value)
            {
                _wishlistCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasWishlist));
                OnPropertyChanged(nameof(WishlistTabTitle));
            }
        }
    }
    public bool HasWishlist => WishlistCount > 0;
    public string WishlistTabTitle => WishlistCount > 0 ? $"{WishlistCount} Wishlist" : "Wishlist";

    private bool _isFriend;
    public bool IsFriend
    {
        get => _isFriend;
        set
        {
            if (_isFriend != value)
            {
                _isFriend = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FriendActionText));
                OnPropertyChanged(nameof(FriendActionGlyph));
                OnPropertyChanged(nameof(FriendStatusTooltip));
            }
        }
    }

    public string FriendActionText => IsFriend ? "Message" : "Demande d'ami";
    public string FriendActionGlyph => IsFriend ? "\uE8BD" : "\uE8FA";
    public string FriendStatusTooltip => IsFriend ? "Envoyer un message" : "Ajouter en ami";

    public bool IsCurrentUser => !string.IsNullOrEmpty(Id) && Id == DiscordClient.CurrentLoggedInUserId;
    public bool IsNotCurrentUser => !IsCurrentUser;

    private string? _friendsSinceText;
    public string? FriendsSinceText
    {
        get => _friendsSinceText;
        set
        {
            if (_friendsSinceText != value)
            {
                _friendsSinceText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFriendsSince));
            }
        }
    }
    public bool HasFriendsSince => !string.IsNullOrWhiteSpace(FriendsSinceText);

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    public bool HasPronouns => !string.IsNullOrWhiteSpace(Pronouns);

    [JsonPropertyName("premium_type")]
    public int PremiumType { get; set; }

    [JsonPropertyName("premium_since")]
    public string? PremiumSince { get; set; }

    [JsonPropertyName("premium_guild_since")]
    public string? PremiumGuildSince { get; set; }

    /// <summary>
    /// Vrai si le compte possède Nitro complet (premium_type == 2) ou si un badge "premium" (Subscriber Nitro) est présent sur le profil.
    /// </summary>
    public bool HasFullNitro => PremiumType == 2 || Badges.Any(b => b.Id.StartsWith("premium", StringComparison.OrdinalIgnoreCase));

    public string MemberSinceText
    {
        get
        {
            if (ulong.TryParse(Id, out ulong snowflake))
            {
                long timestampMs = (long)(snowflake >> 22) + 1420070400000L;
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
                return dt.ToString("MMM dd, yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
            return "Sep 21, 2021";
        }
    }

    [JsonPropertyName("clan")]
    public DiscordClan? Clan { get; set; }

    [JsonPropertyName("primary_guild")]
    public DiscordClan? PrimaryGuild { get; set; }

    public DiscordClan? ActiveClan => Clan ?? PrimaryGuild;
    public bool HasClan => ActiveClan != null && !string.IsNullOrWhiteSpace(ActiveClan.Tag) && (ActiveClan.IdentityEnabled || !string.IsNullOrWhiteSpace(ActiveClan.Badge));
    public string ClanTag => ActiveClan?.Tag ?? string.Empty;
    public string? ClanBadgeUrl => ActiveClan?.BadgeUrl;
    public bool HasClanBadge => !string.IsNullOrEmpty(ClanBadgeUrl);

    public List<DiscordBadge> Badges { get; set; } = new();
    public bool HasBadges => Badges.Count > 0;

    private List<DiscordConnectedAccount> _connectedAccounts = new();
    [JsonPropertyName("connected_accounts")]
    public List<DiscordConnectedAccount> ConnectedAccounts
    {
        get => _connectedAccounts;
        set
        {
            _connectedAccounts = value ?? new();
            foreach (var ca in _connectedAccounts)
            {
                ca.ParentUser = this;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasConnectedAccounts));
        }
    }
    public bool HasConnectedAccounts => ConnectedAccounts.Count > 0;

    public List<DiscordWidgetGame> GameWidgets { get; set; } = new();
    public bool HasGameWidgets => GameWidgets.Count > 0;

    public List<DiscordActivity> Activities { get; set; } = new();

    public DiscordActivity? PrimaryActivity => Activities.FirstOrDefault(a => a.Type != 4 && a.Type != 6);
    public DiscordActivity? CustomStatusActivity => Activities.FirstOrDefault(a => a.Type == 4 && !string.IsNullOrWhiteSpace(a.State));
    public bool HasCustomStatus => CustomStatusActivity is not null && !string.IsNullOrWhiteSpace(CustomStatusActivity.State);
    public string CustomStatusText => CustomStatusActivity?.State ?? string.Empty;
    public bool HasRichPresence => PrimaryActivity is not null;
    public bool HasActivityOrCustomStatus => PrimaryActivity is not null || CustomStatusActivity is not null || IsInVoice;
    public void NotifyActivityTimesChanged()
    {
        PrimaryActivity?.NotifyTimeChanged();
        OnPropertyChanged(nameof(PrimaryActivity));
    }

    public string ActivitySummaryText
    {
        get
        {
            if (IsInVoice)
            {
                return CurrentVoiceChannelName ?? "Salon vocal";
            }
            var act = PrimaryActivity;
            if (act is not null)
            {
                return act.SummaryLine;
            }
            var cs = CustomStatusActivity;
            if (cs is not null && !string.IsNullOrWhiteSpace(cs.State))
            {
                return cs.State;
            }
            return string.Empty;
        }
    }

    public bool HasGameActivity => PrimaryActivity is not null && (PrimaryActivity.Type == 0 || PrimaryActivity.Type == 1);
    public bool HasNonGameActivitySummary => !HasGameActivity && HasActivityOrCustomStatus;
    public string? CurrentGameName => PrimaryActivity?.Name;
    public string? CurrentGameIconUrl => PrimaryActivity?.SmallImageUrl ?? PrimaryActivity?.LargeImageUrl;
    public bool HasCurrentGameIcon => !string.IsNullOrEmpty(CurrentGameIconUrl);
    public string GameTooltipText => PrimaryActivity != null
        ? (PrimaryActivity.Type == 1 ? $"En stream : {PrimaryActivity.Name}" : $"Joue à {PrimaryActivity.Name}")
        : string.Empty;

    [JsonPropertyName("nick")]
    public string? ServerNick { get; set; }

    [JsonPropertyName("guild_avatar")]
    public string? ServerAvatar { get; set; }

    [JsonIgnore]
    public string? GuildIdForAvatar { get; set; }

    public string DisplayName => ServerNick ?? GlobalName ?? Username;

    public void NotifyProfileChanged()
    {
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(GlobalName));
        OnPropertyChanged(nameof(ServerNick));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Avatar));
        OnPropertyChanged(nameof(ServerAvatar));
        OnPropertyChanged(nameof(AvatarUrl));
        OnPropertyChanged(nameof(StaticAvatarUrl));
        OnPropertyChanged(nameof(AnimatedAvatarUrl));
    }

    public bool IsAnimatedAvatar => (ServerAvatar ?? Avatar) is string av && av.StartsWith("a_");
    public bool IsAnimatedBanner => Banner is not null && Banner.StartsWith("a_");

    public string StaticAvatarUrl
    {
        get
        {
            if (ServerAvatar is not null && GuildIdForAvatar is not null)
            {
                return $"https://cdn.discordapp.com/guilds/{GuildIdForAvatar}/users/{Id}/avatars/{ServerAvatar}.png?size=128";
            }
            return Avatar is null
                ? $"https://cdn.discordapp.com/embed/avatars/{(int.TryParse(Discriminator, out var d) ? d % 5 : 0)}.png"
                : $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.png?size=128";
        }
    }

    public string AnimatedAvatarUrl
    {
        get
        {
            if (ServerAvatar is not null && GuildIdForAvatar is not null)
            {
                return $"https://cdn.discordapp.com/guilds/{GuildIdForAvatar}/users/{Id}/avatars/{ServerAvatar}.{(ServerAvatar.StartsWith("a_") ? "gif" : "png")}?size=128";
            }
            return Avatar is null
                ? StaticAvatarUrl
                : $"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.{(Avatar.StartsWith("a_") ? "gif" : "png")}?size=128";
        }
    }

    public string AvatarUrl => AnimatedAvatarUrl;

    public string? StaticBannerUrl => Banner is null
        ? null
        : $"https://cdn.discordapp.com/banners/{Id}/{Banner}.png?size=512";

    public string? AnimatedBannerUrl => Banner is null
        ? null
        : $"https://cdn.discordapp.com/banners/{Id}/{Banner}.{(Banner.StartsWith("a_") ? "gif" : "png")}?size=512";

    public string? BannerUrl => Banner is null
        ? null
        : $"https://cdn.discordapp.com/banners/{Id}/{Banner}.{(Banner.StartsWith("a_") ? "gif" : "png")}?size=512";

    public bool HasBanner => Banner is not null;

    public string AboutMe => string.IsNullOrWhiteSpace(Bio) ? "Grand voyageur de salons Discord." : Bio;

    public string UserTag => Discriminator == "0" ? $"@{Username}" : $"{Username}#{Discriminator}";

    public string Status { get; set; } = "offline";

    private bool _isSpeaking;
    public bool IsSpeaking
    {
        get => _isSpeaking;
        set
        {
            if (_isSpeaking != value)
            {
                _isSpeaking = value;
                OnPropertyChanged(nameof(IsSpeaking));
                OnPropertyChanged(nameof(SpeakingRingOpacity));
                OnPropertyChanged(nameof(SpeakingCardBorderBrush));
                OnPropertyChanged(nameof(SpeakingCardBorderThickness));
            }
        }
    }

    public double SpeakingRingOpacity => _isSpeaking ? 1.0 : 0.0;
    public string SpeakingCardBorderBrush => _isSpeaking ? "#23A55A" : "#33FFFFFF";
    public int SpeakingCardBorderThickness => _isSpeaking ? 3 : 1;

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            if (_isStreaming != value)
            {
                _isStreaming = value;
                OnPropertyChanged(nameof(IsStreaming));
                OnPropertyChanged(nameof(StreamingBadgeOpacity));
            }
        }
    }
    public double StreamingBadgeOpacity => _isStreaming ? 1.0 : 0.0;

    private bool _isVideoEnabled;
    public bool IsVideoEnabled
    {
        get => _isVideoEnabled;
        set
        {
            if (_isVideoEnabled != value)
            {
                _isVideoEnabled = value;
                OnPropertyChanged(nameof(IsVideoEnabled));
                OnPropertyChanged(nameof(VideoBadgeOpacity));
            }
        }
    }
    public double VideoBadgeOpacity => _isVideoEnabled ? 1.0 : 0.0;

    private bool _isVoiceMuted;
    public bool IsVoiceMuted
    {
        get => _isVoiceMuted;
        set
        {
            if (_isVoiceMuted != value)
            {
                _isVoiceMuted = value;
                OnPropertyChanged(nameof(IsVoiceMuted));
                OnPropertyChanged(nameof(VoiceMutedOpacity));
            }
        }
    }
    public double VoiceMutedOpacity => _isVoiceMuted ? 1.0 : 0.0;

    private bool _isVoiceDeafened;
    public bool IsVoiceDeafened
    {
        get => _isVoiceDeafened;
        set
        {
            if (_isVoiceDeafened != value)
            {
                _isVoiceDeafened = value;
                OnPropertyChanged(nameof(IsVoiceDeafened));
                OnPropertyChanged(nameof(VoiceDeafenedOpacity));
            }
        }
    }
    public double VoiceDeafenedOpacity => _isVoiceDeafened ? 1.0 : 0.0;

    public string StatusColor => Status switch
    {
        "online" => "#23A55A",
        "idle" => "#F0B232",
        "dnd" => "#F23F43",
        _ => "#80848E"
    };
}

public sealed class DiscordActivity : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void NotifyTimeChanged()
    {
        OnPropertyChanged(nameof(ElapsedText));
        OnPropertyChanged(nameof(HasElapsed));
    }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("application_id")]
    public string? ApplicationId { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("timestamps")]
    public DiscordActivityTimestamps? Timestamps { get; set; }

    [JsonPropertyName("assets")]
    public DiscordActivityAssets? Assets { get; set; }

    [JsonPropertyName("buttons")]
    public List<string>? Buttons { get; set; }

    public string HeaderLabel => Type switch
    {
        0 => "EN TRAIN DE JOUER",
        1 => "EN DIRECT SUR STREAM",
        2 => $"ÉCOUTE {Name.ToUpperInvariant()}",
        3 => $"REGARDE {Name.ToUpperInvariant()}",
        5 => $"PARTICIPE À {Name.ToUpperInvariant()}",
        _ => "ACTIVITÉ EN COURS"
    };

    public string SummaryLine => Type switch
    {
        0 => $"Joue à {Name}",
        1 => $"En stream : {Details ?? Name}",
        2 => $"Écoute {Details ?? Name}",
        3 => $"Regarde {Name}",
        5 => $"Participe à {Name}",
        _ => Name
    };

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
    public bool HasState => !string.IsNullOrWhiteSpace(State);
    public bool HasLargeImage => !string.IsNullOrWhiteSpace(LargeImageUrl);
    public bool HasSmallImage => !string.IsNullOrWhiteSpace(SmallImageUrl);
    public bool HasElapsed => !string.IsNullOrWhiteSpace(ElapsedText);

    public string RecentSubtitleLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Details)) parts.Add(Details);
            if (!string.IsNullOrWhiteSpace(State)) parts.Add(State);
            if (parts.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(ElapsedText)) parts.Add(ElapsedText);
                else parts.Add("Trending");
            }
            return string.Join(" • ", parts);
        }
    }

    public string EffectiveIconUrl => !string.IsNullOrWhiteSpace(LargeImageUrl)
        ? LargeImageUrl
        : !string.IsNullOrWhiteSpace(SmallImageUrl)
            ? SmallImageUrl
            : !string.IsNullOrWhiteSpace(ApplicationId)
                ? $"https://cdn.discordapp.com/app-icons/{ApplicationId}/icon.png?size=128"
                : string.Empty;

    public bool HasEffectiveIcon => !string.IsNullOrWhiteSpace(EffectiveIconUrl);

    public string? LargeImageUrl => ResolveAssetUrl(ApplicationId, Assets?.LargeImage);
    public string? SmallImageUrl => ResolveAssetUrl(ApplicationId, Assets?.SmallImage);

    public string ElapsedText
    {
        get
        {
            if (Timestamps?.Start is not long startMs || startMs <= 0) return string.Empty;
            var now = DateTimeOffset.UtcNow;
            var start = DateTimeOffset.FromUnixTimeMilliseconds(startMs);
            var elapsed = now - start;
            if (elapsed.TotalSeconds < 0) elapsed = TimeSpan.Zero;

            // Pour Spotify ou musiques avec durée de fin (Timestamps.End)
            if (Timestamps?.End is long endMs && endMs > startMs)
            {
                var total = DateTimeOffset.FromUnixTimeMilliseconds(endMs) - start;
                if (elapsed > total) elapsed = total;
                return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00} / {(int)total.TotalMinutes:00}:{total.Seconds:00}";
            }

            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00} écoulées"
                : $"{elapsed.Minutes:00}:{elapsed.Seconds:00} écoulées";
        }
    }

    private static string? ResolveAssetUrl(string? appId, string? assetKey)
    {
        if (string.IsNullOrWhiteSpace(assetKey)) return null;
        if (assetKey.StartsWith("mp:", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://media.discordapp.net/{assetKey[3..]}";
        }
        if (assetKey.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://i.scdn.co/image/{assetKey[8..]}";
        }
        if (assetKey.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            assetKey.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return assetKey;
        }
        if (!string.IsNullOrWhiteSpace(appId))
        {
            return $"https://cdn.discordapp.com/app-assets/{appId}/{assetKey}.png?size=160";
        }
        return null;
    }
}

public sealed class DiscordActivityTimestamps
{
    [JsonPropertyName("start")]
    public long? Start { get; set; }

    [JsonPropertyName("end")]
    public long? End { get; set; }
}

public sealed class DiscordActivityAssets
{
    [JsonPropertyName("large_image")]
    public string? LargeImage { get; set; }

    [JsonPropertyName("large_text")]
    public string? LargeText { get; set; }

    [JsonPropertyName("small_image")]
    public string? SmallImage { get; set; }

    [JsonPropertyName("small_text")]
    public string? SmallText { get; set; }
}

public sealed class DiscordClan
{
    [JsonPropertyName("identity_guild_id")]
    public string? IdentityGuildId { get; set; }

    [JsonPropertyName("identity_enabled")]
    public bool IdentityEnabled { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("badge")]
    public string? Badge { get; set; }

    public string? BadgeUrl => (!string.IsNullOrWhiteSpace(IdentityGuildId) && !string.IsNullOrWhiteSpace(Badge))
        ? $"https://cdn.discordapp.com/clan-badges/{IdentityGuildId}/{Badge}.png?size=32"
        : null;
}

public sealed class DiscordBadge
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    public string IconUrl => (Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase) || Icon.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase))
        ? Icon
        : $"https://cdn.discordapp.com/badge-icons/{Icon}.png";
}

public sealed class DiscordConnectedAccount
{
    [JsonIgnore]
    public DiscordUser? ParentUser { get; set; }

    public string TextColorHex => (ParentUser?.IsThemeLight == true) ? "#111214" : "#FFFFFF";
    public string SecondaryTextColorHex => (ParentUser?.IsThemeLight == true) ? "#4E5058" : "#949BA4";
    public string CardBackgroundColorHex => (ParentUser?.IsThemeLight == true) ? "#14000000" : "#33000000";
    public string CardBorderColorHex => (ParentUser?.IsThemeLight == true) ? "#22000000" : "#22FFFFFF";
    public string IconBadgeBackgroundColorHex => (ParentUser?.IsThemeLight == true) ? "#E3E5E8" : "#1E1F22";
    public string IconBadgeBorderColorHex => (ParentUser?.IsThemeLight == true) ? "#C4C9CE" : "#383A40";
    public string IconGlyphColorHex => (ParentUser?.IsThemeLight == true) ? "#2E3035" : "#DBDEE1";

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    /// <summary>
    /// Visibilité sur le profil : 1 = visible (par défaut sur /users/{id}/profile), 0 = masqué sur /users/@me/connections.
    /// </summary>
    [JsonPropertyName("visibility")]
    public int Visibility { get; set; } = 1;

    public string PlatformDisplayName => Type.ToLowerInvariant() switch
    {
        "github" => "GitHub",
        "steam" => "Steam",
        "spotify" => "Spotify",
        "twitch" => "Twitch",
        "youtube" => "YouTube",
        "twitter" or "x" => "X (Twitter)",
        "reddit" => "Reddit",
        "playstation" => "PlayStation Network",
        "xbox" => "Xbox",
        "battlenet" => "Battle.net",
        "riotgames" => "Riot Games",
        "epicgames" => "Epic Games",
        "roblox" => "Roblox",
        "tiktok" => "TikTok",
        "instagram" => "Instagram",
        "bluesky" => "Bluesky",
        "mastodon" => "Mastodon",
        "paypal" => "PayPal",
        "ebay" => "eBay",
        "crunchyroll" => "Crunchyroll",
        "domain" => "Site Web / Domaine",
        "bungie" => "Bungie.net",
        "leagueoflegends" => "League of Legends",
        _ => string.IsNullOrWhiteSpace(Type) ? "Compte lié" : char.ToUpperInvariant(Type[0]) + Type[1..]
    };

    public string PlatformBubbleName => Type.ToLowerInvariant() switch
    {
        "playstation" => "PlayStation Network",
        "xbox" => "Xbox",
        "riotgames" => "Riot Games",
        "steam" => "Steam",
        "spotify" => "Spotify",
        "github" => "GitHub",
        "twitch" => "Twitch",
        "youtube" => "YouTube",
        "twitter" or "x" => "X",
        "reddit" => "Reddit",
        "domain" => "Site Web / Domaine",
        "roblox" => "Roblox",
        "battlenet" => "Battle.net",
        "tiktok" => "TikTok",
        "instagram" => "Instagram",
        _ => PlatformDisplayName
    };

    public string PlatformPathData => Type.ToLowerInvariant() switch
    {
        // PlayStation Network
        "playstation" => "M23.67 17.26c-.36-.92-1.72-1.61-3.62-1.84v-4.14c0-.98-.12-1.87-.9-2.31-.69-.4-1.63-.25-2.22.4l-3.37 3.84v-7.1C13.56 5.06 13 4 11.83 4c-1.12 0-1.75.95-1.75 2.11v11.96l-3.21-1.5c-1.2-.56-2.45-.73-3.08-.22-.8.64-.89 1.83-.2 2.68.73.91 2.53 1.75 4.7 2.22l2.48.55v-2.02l-1.95-.43c-1.41-.31-2.4-.76-2.67-1.17-.18-.28-.08-.59.25-.7.42-.14 1.25.04 2.2.49l2.17 1.01v-2.33l-1.44-.67c-1.45-.67-2.6-.96-3.32-.82v-.01c.91-.4 2.27-.12 3.86.62l.9.42V6.11c0-.46.16-.76.54-.76.36 0 .54.3.54.76v7.35l4.35-5.02c.3-.34.72-.51 1.1-.4.44.13.57.59.57 1.15v3.86c-1.24-.13-2.52-.16-3.81-.08l-.87.05v2.24l.97-.04c1.38-.06 2.76.01 4.02.2 1.48.22 2.7.67 3.2 1.18.35.36.29.77-.16 1.02-.63.35-1.84.44-3.23.23l-2.07-.31v2.16l2.15.33c2.05.31 3.73.18 4.76-.5.98-.65 1.12-1.75.45-2.68z",
        // Xbox
        "xbox" => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm4.88 4.25c1.47 1.29 2.47 3.03 2.82 4.97-1.1-1.28-2.64-2.4-4.52-3.17 1.1-.9 1.7-1.8 1.7-1.8zm-9.76 0s.6.9 1.7 1.8c-1.88.77-3.42 1.89-4.52 3.17.35-1.94 1.35-3.68 2.82-4.97zM12 7.75c1.9 1.3 3.66 3.16 4.98 5.37-1.54 1.83-3.26 3.1-4.98 3.67-1.72-.57-3.44-1.84-4.98-3.67 1.32-2.21 3.08-4.07 4.98-5.37zM4.15 13.84c.82 1.11 2.06 2.37 3.63 3.42-1.33.35-2.73.1-3.63-.5-.45-.3-.7-.6-.82-.87.2-.72.48-1.42.82-2.05zm15.7 0c.34.63.62 1.33.82 2.05-.12.27-.37.57-.82.87-.9.6-2.3.85-3.63.5 1.57-1.05 2.81-2.31 3.63-3.42z",
        // Steam
        "steam" => "M12 2a10 10 0 0 0-10 9.87l5.3 2.18c.43-.37.98-.6 1.58-.64l2.4-3.47v-.04a3.1 3.1 0 1 1 3.1 3.1h-.04l-3.47 2.4a2.22 2.22 0 0 1-.64 1.58L8 21.8A10 10 0 1 0 12 2zm0 4.67a2.33 2.33 0 1 0 2.33 2.33A2.33 2.33 0 0 0 12 6.67zm-3.12 9.08l-2.05-.84a3.02 3.02 0 0 0-.58 1.84 3 3 0 0 0 3 3 3.02 3.02 0 0 0 1.84-.58l-.84-2.05a1.53 1.53 0 0 1-1.37-1.37z",
        // Riot Games
        "riotgames" => "M13.7 3.5L3.8 8.1l2.5 10.9 4-1.2-.8-3.6 2.4-.7 1 4.5 4.5-1.4 3.7-11.2-7.4-1.9zm-.9 4.3l3.6 1-.9 2.7-3.6-1 .9-2.7z",
        // Spotify
        "spotify" => "M12 2C6.477 2 2 6.477 2 12s4.477 10 10 10 10-4.477 10-10S17.523 2 12 2zm4.586 14.424c-.18.295-.563.387-.857.207-2.35-1.434-5.308-1.758-8.793-.963-.335.077-.67-.133-.746-.469-.077-.334.132-.67.467-.746 3.808-.87 7.076-.51 9.721 1.114.294.18.388.563.208.857zm1.226-2.724c-.227.369-.714.484-1.083.257-2.69-1.654-6.79-2.133-9.97-1.168-.413.125-.851-.107-.975-.52-.125-.414.107-.852.52-.976 3.633-1.103 8.147-.568 11.25 1.341.369.227.485.714.258 1.066zm.106-2.835C14.692 8.95 9.375 8.775 6.297 9.71c-.494.15-1.018-.129-1.168-.623-.15-.495.129-1.019.624-1.169 3.532-1.072 9.404-.866 13.115 1.337.445.264.59.838.327 1.282-.264.443-.838.59-1.282.327z",
        // GitHub
        "github" => "M12 2A10 10 0 0 0 2 12c0 4.42 2.87 8.17 6.84 9.5.5.08.66-.23.66-.5v-1.69c-2.77.6-3.36-1.34-3.36-1.34-.46-1.16-1.11-1.47-1.11-1.47-.91-.62.07-.6.07-.6 1 .07 1.53 1.03 1.53 1.03.87 1.52 2.34 1.07 2.91.83.1-.65.35-1.09.63-1.34-2.22-.25-4.55-1.11-4.55-4.92 0-1.11.38-2 1.03-2.71-.1-.25-.45-1.29.1-2.64 0 0 .84-.27 2.75 1.02.79-.22 1.65-.33 2.5-.33.85 0 1.71.11 2.5.33 1.91-1.29 2.75-1.02 2.75-1.02.55 1.35.2 2.39.1 2.64.65.71 1.03 1.6 1.03 2.71 0 3.82-2.34 4.66-4.57 4.91.36.31.69.92.69 1.85V21c0 .27.16.59.67.5C19.14 20.16 22 16.42 22 12A10 10 0 0 0 12 2z",
        // YouTube
        "youtube" => "M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z",
        // Twitch
        "twitch" => "M2.149 0L.537 4.119v16.836h5.731V24h3.224l3.045-3.045h4.657l6.269-6.269V0H2.149zm19.164 13.612l-3.582 3.582H12l-3.045 3.045v-3.045H4.836V1.791h16.477v11.821zM16.477 5.373h-2.149v6.448h2.149V5.373zm-5.731 0H8.597v6.448h2.149V5.373z",
        // X / Twitter
        "twitter" or "x" => "M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z",
        // Reddit
        "reddit" => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm6.2 8.2a1.8 1.8 0 0 1 .8 1.5 1.8 1.8 0 0 1-1.1 1.7 6.4 6.4 0 0 1-5.9 2.6 6.4 6.4 0 0 1-5.9-2.6 1.8 1.8 0 0 1-1.1-1.7 1.8 1.8 0 0 1 .8-1.5 6.7 6.7 0 0 1 3.5-1.4l.7-3.3 2.3.5a1.2 1.2 0 1 1 2.3.4l-2.8-.6-.6 2.8a6.7 6.7 0 0 1 3.5 1.4zM9 13a1 1 0 1 0 1-1 1 1 0 0 0-1 1zm6 0a1 1 0 1 0 1-1 1 1 0 0 0-1 1zm-4.8 2.2a.4.4 0 0 0 0 .6 2.6 2.6 0 0 0 3.6 0 .4.4 0 1 0-.6-.6 1.8 1.8 0 0 1-2.4 0 .4.4 0 0 0-.6 0z",
        // Domain / Web (Globe Discord officiel)
        _ => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm7.93 9h-3.18a15.7 15.7 0 0 0-1.38-5.38A8.03 8.03 0 0 1 19.93 11zM12 4.04c.83 1.2 1.48 3.5 1.7 4.96h-3.4c.22-1.46.87-3.76 1.7-4.96zM4.07 13h3.18a15.7 15.7 0 0 0 1.38 5.38A8.03 8.03 0 0 1 4.07 13zm3.18-2H4.07a8.03 8.03 0 0 1 4.56-5.38A15.7 15.7 0 0 0 7.25 11zm3.05 2h3.4c-.22 1.46-.87 3.76-1.7 4.96-.83-1.2-1.48-3.5-1.7-4.96zm4.45 5.38A15.7 15.7 0 0 0 16.13 13h3.18a8.03 8.03 0 0 1-4.56 5.38z"
    };

    public string PlatformGlyph => Type.ToLowerInvariant() switch
    {
        "spotify" => "\uEC4F",
        "youtube" => "\uE768",
        "twitch" => "\uE714",
        "steam" or "playstation" or "xbox" or "battlenet" or "riotgames" or "epicgames" or "roblox" => "\uE7FC",
        "github" => "\uE943",
        "twitter" or "x" => "\uE110",
        "domain" => "\uE774",
        "tiktok" or "instagram" => "\uEB9F",
        "bluesky" => "\uE774",
        _ => "\uE774"
    };

    public string PlatformBadgeText => Type.ToLowerInvariant() switch
    {
        "github" => "GH",
        "steam" => "ST",
        "spotify" => "SP",
        "twitch" => "TW",
        "youtube" => "YT",
        "twitter" or "x" => "X",
        "reddit" => "RD",
        "playstation" => "PS",
        "xbox" => "XB",
        "battlenet" => "BN",
        "riotgames" => "RG",
        "epicgames" => "EG",
        "roblox" => "RBX",
        "tiktok" => "TT",
        "instagram" => "IG",
        "bluesky" => "BS",
        "domain" => "WEB",
        _ => "LINK"
    };

    public string? ProfileUrl => Type.ToLowerInvariant() switch
    {
        "github" when !string.IsNullOrWhiteSpace(Name) => $"https://github.com/{Name}",
        "steam" when !string.IsNullOrWhiteSpace(Id) => $"https://steamcommunity.com/profiles/{Id}",
        "spotify" when !string.IsNullOrWhiteSpace(Id) => $"https://open.spotify.com/user/{Id}",
        "twitch" when !string.IsNullOrWhiteSpace(Name) => $"https://www.twitch.tv/{Name}",
        "youtube" when !string.IsNullOrWhiteSpace(Id) => $"https://www.youtube.com/channel/{Id}",
        "twitter" or "x" when !string.IsNullOrWhiteSpace(Name) => $"https://x.com/{Name}",
        "reddit" when !string.IsNullOrWhiteSpace(Name) => $"https://www.reddit.com/user/{Name}",
        "tiktok" when !string.IsNullOrWhiteSpace(Name) => $"https://www.tiktok.com/@{Name.TrimStart('@')}",
        "instagram" when !string.IsNullOrWhiteSpace(Name) => $"https://www.instagram.com/{Name.TrimStart('@')}",
        "bluesky" when !string.IsNullOrWhiteSpace(Name) => $"https://bsky.app/profile/{Name.TrimStart('@')}",
        "roblox" when !string.IsNullOrWhiteSpace(Id) => $"https://www.roblox.com/users/{Id}/profile",
        "domain" when !string.IsNullOrWhiteSpace(Name) => Name.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? Name : $"https://{Name}",
        _ when Name.Contains('.') && !Name.Contains(' ') => Name.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? Name : $"https://{Name}",
        _ => null
    };

    public bool HasUrl => !string.IsNullOrWhiteSpace(ProfileUrl);
    public string TooltipText => HasUrl ? $"{PlatformDisplayName} : {Name}\nCliquez pour ouvrir {ProfileUrl}" : $"{PlatformDisplayName} : {Name} (cliquez pour copier)";
}

public sealed class DiscordWidgetGame
{
    public string GameId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public bool IsFavorite { get; set; }
    public string TooltipText => string.IsNullOrWhiteSpace(Comment) ? Name : $"{Name} — \"{Comment}\"";
}

public sealed class DiscordMutualGuild : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Initials));
            }
        }
    }

    private string? _iconUrl;
    public string? IconUrl
    {
        get => _iconUrl;
        set
        {
            if (_iconUrl != value)
            {
                _iconUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(IconUrl);
    public string Initials => string.IsNullOrWhiteSpace(Name) ? "?" : string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
}
