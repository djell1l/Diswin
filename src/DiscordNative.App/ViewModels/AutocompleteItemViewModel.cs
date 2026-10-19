using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DiscordNative.App.ViewModels;

public enum AutocompleteItemType
{
    Member,
    Role,
    SpecialMention,
    Emoji
}

public sealed class AutocompleteItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public AutocompleteItemType ItemType { get; init; } = AutocompleteItemType.Member;

    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    public string? AvatarUrl { get; init; }
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    public string? StatusColor { get; init; }
    public bool HasStatus => !string.IsNullOrEmpty(StatusColor);

    public string? RoleColorHex { get; init; }
    public bool HasRoleColor => !string.IsNullOrEmpty(RoleColorHex);
    public string TitleColorHex => HasRoleColor ? RoleColorHex! : "#F2F3F5";

    public bool IsEmoji => ItemType == AutocompleteItemType.Emoji;
    public bool IsCustomEmoji { get; init; }
    public string? EmojiUnicode { get; init; }
    public string? EmojiImageUrl { get; init; }

    public string TextToInsert { get; init; } = string.Empty;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundHex));
            }
        }
    }

    public string BackgroundHex => _isSelected ? "#35373C" : "Transparent";
}
