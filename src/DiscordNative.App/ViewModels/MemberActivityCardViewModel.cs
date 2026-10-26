using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DiscordNative.Protocol;
using DiscordNative.Protocol.Models;

namespace DiscordNative.App.ViewModels;

public sealed class MemberActivityCardViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public DiscordUser User { get; }
    public DiscordActivity Activity { get; }
    public string? VoiceChannelName { get; }
    public string? CustomTimeAgoText { get; }

    public MemberActivityCardViewModel(DiscordUser user, DiscordActivity activity, string? voiceChannelName = null, string? customTimeAgoText = null)
    {
        User = user;
        Activity = activity;
        VoiceChannelName = voiceChannelName;
        CustomTimeAgoText = customTimeAgoText;

        if (!string.IsNullOrEmpty(activity.ApplicationId) && !HasLargeImage)
        {
            _ = ResolveAppRpcAsync(activity.ApplicationId);
        }
    }

    private async Task ResolveAppRpcAsync(string appId)
    {
        var app = await DiscordApplicationCache.FetchApplicationRpcAsync(appId);
        if (app != null)
        {
            Protocol.App.RunOnUiThread(() =>
            {
                OnPropertyChanged(nameof(LargeImageUrl));
                OnPropertyChanged(nameof(CoverImageUrl));
                OnPropertyChanged(nameof(HasLargeImage));
            });
        }
    }

    public string UserName => User.DisplayName;
    public string UserRoleColor => User.RoleColorHex;
    public string UserStatusColor => User.StatusColor;
    public string UserAvatarUrl => User.AvatarUrl;
    public bool HasUserAvatar => User.HasAvatar;
    public string UserInitials => string.IsNullOrWhiteSpace(User.DisplayName) ? "?" : User.DisplayName[..1].ToUpperInvariant();

    public string ActivityName => Activity.Name;
    public string? LargeImageUrl => Activity.LargeImageUrl ?? Activity.SmallImageUrl ?? Activity.CoverImageUrl ?? User.CurrentGameIconUrl;
    public string? CoverImageUrl => Activity.CoverImageUrl ?? LargeImageUrl;
    public bool HasLargeImage => !string.IsNullOrWhiteSpace(LargeImageUrl);

    public string SubtitleLine
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(CustomTimeAgoText))
            {
                parts.Add(CustomTimeAgoText);
            }
            else if (!string.IsNullOrWhiteSpace(Activity.ElapsedText))
            {
                parts.Add(Activity.ElapsedText);
            }
            else if (!string.IsNullOrWhiteSpace(Activity.Details))
            {
                parts.Add(Activity.Details);
            }

            if (!string.IsNullOrWhiteSpace(VoiceChannelName))
            {
                parts.Add(VoiceChannelName);
            }
            else if (!string.IsNullOrWhiteSpace(Activity.State))
            {
                parts.Add(Activity.State);
            }

            return parts.Count > 0 ? string.Join("  •  ", parts) : "En cours";
        }
    }

    public bool HasVoiceChannel => !string.IsNullOrWhiteSpace(VoiceChannelName);

    public string TooltipText => $"{User.DisplayName}\n{Activity.Name}\n{SubtitleLine}";
}
