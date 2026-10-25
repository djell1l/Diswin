using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace DiscordNative.App.Views;

public sealed partial class TrayPopupWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly IntPtr _hWnd;
    private readonly AppWindow _appWindow;

    private const int PopupWidth = 280;
    private const int PopupHeight = 408;

    public TrayPopupWindow(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.IsShownInSwitchers = false;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // Masquer de la barre des tâches via WS_EX_TOOLWINDOW
        int exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
        SetWindowLong(_hWnd, GWL_EXSTYLE, (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);

        Activated += OnWindowActivated;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HidePopup();
        }
    }

    public void ShowAtCursor()
    {
        RefreshState();

        GetCursorPos(out POINT pt);
        var displayArea = DisplayArea.GetFromPoint(new PointInt32(pt.X, pt.Y), DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        int height = _mainWindow.ViewModel.IsVoiceConnected ? PopupHeight + 36 : PopupHeight;
        int x = Math.Clamp(pt.X - (PopupWidth / 2), workArea.X + 8, workArea.X + workArea.Width - PopupWidth - 8);
        int y = Math.Clamp(pt.Y - height - 10, workArea.Y + 8, workArea.Y + workArea.Height - height - 8);

        _appWindow.MoveAndResize(new RectInt32(x, y, PopupWidth, height));
        _appWindow.Show();
        SetForegroundWindow(_hWnd);
        Activate();
    }

    public void HidePopup()
    {
        try
        {
            _appWindow.Hide();
        }
        catch { }
    }

    private void RefreshState()
    {
        var vm = _mainWindow.ViewModel;
        UserDisplayNameText.Text = !string.IsNullOrWhiteSpace(vm.CurrentUserName) ? vm.CurrentUserName : "Diswin";

        if (!string.IsNullOrEmpty(vm.CurrentUserAvatarUrl))
        {
            try { UserAvatar.ProfilePicture = new BitmapImage(new Uri(vm.CurrentUserAvatarUrl)); } catch { }
        }

        var (statusLabel, colorHex) = vm.CurrentStatus switch
        {
            "idle" => ("Inactif", "#F0B232"),
            "dnd" => ("Ne pas déranger", "#F23F43"),
            "invisible" => ("Invisible", "#80848E"),
            _ => ("En ligne", "#23A55A")
        };

        UserStatusText.Text = vm.IsVoiceConnected
            ? $"En vocal • {vm.ConnectedVoiceChannelName}"
            : statusLabel;

        StatusDot.Fill = new SolidColorBrush(ParseColor(colorHex));

        MuteMenuText.Text = vm.IsMuted ? "Réactiver le microphone" : "Couper le microphone";
        MuteIcon.Glyph = vm.IsMuted ? "\uF781" : "\uE720";
        MuteIcon.Foreground = new SolidColorBrush(ParseColor(vm.IsMuted ? "#F23F43" : "#B5BAC1"));

        DeafenMenuText.Text = vm.IsDeafened ? "Réactiver le son (Casque)" : "Mettre en sourdine (Casque)";
        DeafenIcon.Foreground = new SolidColorBrush(ParseColor(vm.IsDeafened ? "#F23F43" : "#B5BAC1"));

        DisconnectVoiceButton.Visibility = vm.IsVoiceConnected ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            return Windows.UI.Color.FromArgb(255,
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        }
        return Windows.UI.Color.FromArgb(255, 88, 101, 242);
    }

    private void OnOpenDiswinClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ShowAndActivate();
    }

    private void OnQuickSwitcherClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ShowAndActivate();
        _mainWindow.OpenQuickSwitcher();
    }

    private void OnStatusClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string status })
        {
            _mainWindow.ViewModel.SetStatus(status);
            RefreshState();
        }
    }

    private void OnToggleMuteClicked(object sender, RoutedEventArgs e)
    {
        _mainWindow.ViewModel.ToggleMute();
        RefreshState();
    }

    private void OnToggleDeafenClicked(object sender, RoutedEventArgs e)
    {
        _mainWindow.ViewModel.ToggleDeafen();
        RefreshState();
    }

    private void OnDisconnectVoiceClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ViewModel.DisconnectVoice();
    }

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ShowAndActivate();
        var win = new SettingsWindow(_mainWindow.ViewModel);
        win.Activate();
    }

    private void OnQuitDiswinClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ExitApplication();
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
