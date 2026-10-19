using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DiscordNative.Protocol.Models;

public sealed class DiscordMemberGroup : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public string Name { get; set; } = string.Empty;
    public string RoleColorHex { get; set; } = "#949BA4";
    public int Position { get; set; }

    public ObservableCollection<DiscordUser> Members { get; } = new();

    public int Count => Members.Count;

    public string HeaderText => $"{Name.ToUpperInvariant()} — {Count}";

    public void NotifyMembersChanged()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HeaderText));
    }
}
