using System;
using System.Runtime.InteropServices;
using DiscordNative.App.Services;
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

    private const int PopupWidth = 264;

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

        // Supprimer complètement la bordure blanche du système et activer les coins arrondis sombres
        ApplyDwmWindowAttributes(_hWnd);

        // Masquer de la barre des tâches via WS_EX_TOOLWINDOW
        int exStyle = GetWindowLong(_hWnd, GWL_EXSTYLE);
        SetWindowLong(_hWnd, GWL_EXSTYLE, (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);

        Activated += OnWindowActivated;
    }

    private static void ApplyDwmWindowAttributes(IntPtr hWnd)
    {
        try
        {
            int darkMode = 1;
            // Supporte Windows 10 (19) et Windows 11 / Windows 10 20H1+ (20)
            DwmSetWindowAttribute(hWnd, 19, ref darkMode, sizeof(int));
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Arrondi Windows 11 natif
            int cornerPreference = DWMWCP_ROUND;
            DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Supprime totalement la bordure DWM de la fenêtre (aucun contour blanc)
            int borderColor = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hWnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
        }
        catch { }
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

        // Récupérer le facteur d'échelle DPI de l'écran pour convertir les DIPs XAML en pixels physiques
        uint dpi = GetDpiForWindow(_hWnd);
        double dpiScale = (dpi > 0 ? dpi : 96) / 96.0;

        // Mesurer la taille exacte nécessaire du contenu (en DIPs) avec une marge de sécurité
        RootBorder.Measure(new Windows.Foundation.Size(PopupWidth, double.PositiveInfinity));
        double dipHeight = Math.Max(RootBorder.DesiredSize.Height + 12, 330);

        // Convertir en pixels physiques pour AppWindow.MoveAndResize
        int physicalWidth = (int)Math.Ceiling(PopupWidth * dpiScale);
        int physicalHeight = (int)Math.Ceiling(dipHeight * dpiScale);

        int x = Math.Clamp(pt.X - (physicalWidth / 2), workArea.X + 8, workArea.X + workArea.Width - physicalWidth - 8);
        int y = Math.Clamp(pt.Y - physicalHeight - (int)(12 * dpiScale), workArea.Y + 8, workArea.Y + workArea.Height - physicalHeight - 8);

        _appWindow.MoveAndResize(new RectInt32(x, y, physicalWidth, physicalHeight));
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
            "dnd" => ("Occupé", "#F23F43"),
            "invisible" => ("Invisible", "#80848E"),
            _ => ("En ligne", "#23A55A")
        };

        UserStatusText.Text = vm.IsVoiceConnected
            ? $"En vocal • {vm.ConnectedVoiceChannelName}"
            : statusLabel;

        StatusDot.Fill = new SolidColorBrush(ParseColor(colorHex));

        MuteMenuText.Text = vm.IsMuted ? "Réactiver le micro" : "Couper le micro";
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

    private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
    {
        HidePopup();
        _mainWindow.ShowAndActivate();
        await _mainWindow.CheckForUpdatesAsync();
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

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
