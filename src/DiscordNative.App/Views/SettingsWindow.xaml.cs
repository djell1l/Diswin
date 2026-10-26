using DiscordNative.App.Services;
using DiscordNative.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DiscordNative.App.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsService _settings = App.Settings;
    private readonly MainViewModel? _viewModel;
    private bool _isInitializing = true;

    public SettingsWindow(MainViewModel? viewModel = null)
    {
        _viewModel = viewModel;
        InitializeComponent();
        Title = "Diswin — Paramètres";
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        SetTitleBar(AppTitleBar);
        SetWindowIcon();

        LoadCurrentSettings();
        _isInitializing = false;
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;

        AppearanceSection.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
        ProfilesSection.Visibility = tag == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        VoiceSection.Visibility = tag == "Voice" ? Visibility.Visible : Visibility.Collapsed;
        MessagingSection.Visibility = tag == "Messaging" ? Visibility.Visible : Visibility.Collapsed;
        KeybindsSection.Visibility = tag == "Keybinds" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesSection.Visibility = tag == "Updates" ? Visibility.Visible : Visibility.Collapsed;
        AccountSection.Visibility = tag == "Account" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetWindowIcon()
    {
        try
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
                MainWindow.ApplyWin32WindowIcon(hWnd, iconPath);
            }
        }
        catch { }
    }

    private void LoadCurrentSettings()
    {
        var current = _settings.Current;

        ThemeCombo.SelectedItem = ThemeCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == current.ThemeMode) ?? ThemeCombo.Items[0];

        LanguageCombo.SelectedItem = LanguageCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == current.Language) ?? LanguageCombo.Items[0];

        UpdateChannelCombo.SelectedItem = UpdateChannelCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals((string)i.Tag, current.UpdateChannel, StringComparison.OrdinalIgnoreCase)
                              || (string.Equals((string)i.Tag, "Edge", StringComparison.OrdinalIgnoreCase) && string.Equals(current.UpdateChannel, "Experimental", StringComparison.OrdinalIgnoreCase)))
            ?? UpdateChannelCombo.Items[0];

        ScaleSlider.Value = Math.Clamp(current.InterfaceScale, 0.85, 1.25);
        BubbleStyleToggle.IsOn = current.BubbleMessageStyle;
        CompactModeToggle.IsOn = current.CompactMode;
        ForceAccentColorOnProfilesToggle.IsOn = current.ForceAccentColorOnProfileCards;
        MinimizeToTrayToggle.IsOn = current.MinimizeToTrayOnClose;
        ShowNotificationsToggle.IsOn = current.ShowNotifications;
        AnonymizeUploadsToggle.IsOn = current.AnonymizeUploads;
        ConvertLongMessagesToggle.IsOn = current.ConvertLongMessagesToFile;
        KrispToggle.IsOn = current.EnableKrispNoiseSuppression;
        UpdateKrispStatusText();

        InputVolumeSlider.Value = Math.Clamp(current.InputVolume * 100.0, 0, 100);
        InputVolumeLabel.Text = $"{(int)(current.InputVolume * 100.0)}%";
        OutputVolumeSlider.Value = Math.Clamp(current.OutputVolume * 100.0, 0, 200);
        OutputVolumeLabel.Text = $"{(int)(current.OutputVolume * 100.0)}%";

        InputModeCombo.SelectedItem = InputModeCombo.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == current.InputMode) ?? InputModeCombo.Items[0];

        AutoSensitivityToggle.IsOn = current.AutoVoiceSensitivity;
        SensitivitySlider.Value = Math.Clamp(current.VoiceSensitivityLevel, 0, 100);
        SensitivityLabel.Text = $"-{(int)(100 - current.VoiceSensitivityLevel)} dB";
        ManualSensitivitySliderPanel.Visibility = current.AutoVoiceSensitivity ? Visibility.Collapsed : Visibility.Visible;
        VoiceSensitivityPanel.Visibility = current.InputMode == "PushToTalk" ? Visibility.Collapsed : Visibility.Visible;

        EchoCancellationToggle.IsOn = current.EchoCancellation;
        AdvancedNoiseReductionToggle.IsOn = current.AdvancedNoiseReduction;

        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalInputVolume = (float)current.InputVolume;
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalOutputVolume = (float)current.OutputVolume;
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalAutoVoiceSensitivity = current.AutoVoiceSensitivity;
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalVoiceSensitivityLevel = current.VoiceSensitivityLevel;
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalInputMode = current.InputMode;

        OpacitySlider.Value = current.BackgroundOpacity;
        BlurSlider.Value = current.BackgroundBlurRadius;

        if (!string.IsNullOrEmpty(current.BackgroundImagePath) && System.IO.File.Exists(current.BackgroundImagePath))
        {
            BackgroundPreview.Source = new BitmapImage(new Uri(current.BackgroundImagePath));
        }
    }

    private void UpdateKrispStatusText()
    {
        if (KrispEngineStatusText is null) return;
        using var _ = new DiscordNative.Protocol.Voice.KrispAudioProcessor();
        bool nativeOk = DiscordNative.Protocol.Voice.KrispAudioProcessor.IsNativeKrispLoaded;
        string engineDesc = DiscordNative.Protocol.Voice.KrispAudioProcessor.LoadedEngineDescription;

        if (!_settings.Current.EnableKrispNoiseSuppression)
        {
            if (KrispEngineStatusIcon is not null) KrispEngineStatusIcon.Visibility = Visibility.Collapsed;
            KrispEngineStatusText.Text = "Krisp est actuellement désactivé.";
        }
        else if (nativeOk)
        {
            if (KrispEngineStatusIcon is not null) KrispEngineStatusIcon.Visibility = Visibility.Visible;
            KrispEngineStatusText.Text = $"Moteur actif : {engineDesc}";
        }
        else
        {
            if (KrispEngineStatusIcon is not null) KrispEngineStatusIcon.Visibility = Visibility.Visible;
            KrispEngineStatusText.Text = "Filtre de suppression de bruit adaptatif actif (DSP 48kHz)";
        }
    }

    private void OnKrispToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.EnableKrispNoiseSuppression = KrispToggle.IsOn;
        _settings.Save();
        UpdateKrispStatusText();
        if (_viewModel is not null)
        {
            _viewModel.SetKrispEnabled(KrispToggle.IsOn);
        }
        else if (App.MainAppWindow is MainWindow mw)
        {
            mw.ViewModel.SetKrispEnabled(KrispToggle.IsOn);
        }
    }

    private void OnInputVolumeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.InputVolume = e.NewValue / 100.0;
        InputVolumeLabel.Text = $"{(int)e.NewValue}%";
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalInputVolume = (float)_settings.Current.InputVolume;
        _settings.Save();
    }

    private void OnOutputVolumeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.OutputVolume = e.NewValue / 100.0;
        OutputVolumeLabel.Text = $"{(int)e.NewValue}%";
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalOutputVolume = (float)_settings.Current.OutputVolume;
        _settings.Save();
    }

    private void OnInputModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (InputModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _settings.Current.InputMode = tag;
            DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalInputMode = tag;
            VoiceSensitivityPanel.Visibility = tag == "PushToTalk" ? Visibility.Collapsed : Visibility.Visible;
            _settings.Save();
        }
    }

    private void OnAutoSensitivityToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.AutoVoiceSensitivity = AutoSensitivityToggle.IsOn;
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalAutoVoiceSensitivity = AutoSensitivityToggle.IsOn;
        ManualSensitivitySliderPanel.Visibility = AutoSensitivityToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
        _settings.Save();
    }

    private void OnSensitivityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.VoiceSensitivityLevel = e.NewValue;
        SensitivityLabel.Text = $"-{(int)(100 - e.NewValue)} dB";
        DiscordNative.Protocol.Voice.DiscordVoiceClient.GlobalVoiceSensitivityLevel = e.NewValue;
        _settings.Save();
    }

    private void OnEchoCancellationToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.EchoCancellation = EchoCancellationToggle.IsOn;
        _settings.Save();
    }

    private void OnAdvancedNoiseReductionToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.AdvancedNoiseReduction = AdvancedNoiseReductionToggle.IsOn;
        _settings.Save();
    }

    private void OnForceAccentColorOnProfilesToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.ForceAccentColorOnProfileCards = ForceAccentColorOnProfilesToggle.IsOn;
        _settings.Save();
    }

    private void OnMinimizeToTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.MinimizeToTrayOnClose = MinimizeToTrayToggle.IsOn;
        _settings.Save();
    }

    private void OnShowNotificationsToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.ShowNotifications = ShowNotificationsToggle.IsOn;
        _settings.Save();
    }

    private async void OnChooseBackgroundClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".gif");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        var path = await _settings.ImportBackgroundImageAsync(file);
        BackgroundPreview.Source = new BitmapImage(new Uri(path));
    }

    private void OnResetBackgroundClicked(object sender, RoutedEventArgs e)
    {
        _settings.ClearBackgroundImage();
        BackgroundPreview.Source = null;
    }

    private void OnScaleChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.InterfaceScale = e.NewValue;
        _settings.Save();
    }

    private void OnBubbleStyleToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.BubbleMessageStyle = BubbleStyleToggle.IsOn;
        _settings.Save();
    }

    private void OnAnonymizeUploadsToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.AnonymizeUploads = AnonymizeUploadsToggle.IsOn;
        _settings.Save();
    }

    private void OnConvertLongMessagesToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.ConvertLongMessagesToFile = ConvertLongMessagesToggle.IsOn;
        _settings.Save();
    }

    private void OnUpdateChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (UpdateChannelCombo.SelectedItem is ComboBoxItem item)
        {
            _settings.Current.UpdateChannel = (string)item.Tag;
            _settings.Save();
        }
    }

    private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateCheckStatusText.Text = "Recherche de mises à jour sur GitHub...";
        try
        {
            var update = await UpdateService.CheckForUpdatesAsync();
            if (update is not null)
            {
                UpdateCheckStatusText.Text = $"Nouvelle version disponible : {update.TagName}";
                if (App.MainAppWindow is MainWindow mw && mw.Content is MainWindowContent mwc)
                {
                    mwc.SetAvailableUpdate(update);
                }

                var dlg = new ContentDialog
                {
                    Title = $"Mise à jour disponible — {update.TagName}",
                    Content = $"Une nouvelle version ({update.TagName}) est disponible.\nDiswin est open-source : la mise à jour est facultative. Voulez-vous mettre à jour et redémarrer maintenant ?",
                    PrimaryButtonText = "Mettre à jour et redémarrer",
                    CloseButtonText = "Plus tard",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot
                };
                if (await dlg.ShowAsync() == ContentDialogResult.Primary)
                {
                    await UpdateService.ApplyAutomaticUpdateAsync(update, status =>
                    {
                        Protocol.App.RunOnUiThread(() => UpdateCheckStatusText.Text = status);
                    });
                }
            }
            else
            {
                var channelLabel = string.Equals(_settings.Current.UpdateChannel, "Edge", StringComparison.OrdinalIgnoreCase) ? "Edge" : "Stable";
                UpdateCheckStatusText.Text = $"Diswin est à jour ({UpdateService.CurrentVersion} — Canal {channelLabel}).";
            }
        }
        catch
        {
            UpdateCheckStatusText.Text = "Impossible de vérifier les mises à jour.";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async void OnExportSettingsClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedFileName = "diswin-settings";
        picker.FileTypeChoices.Add("JSON Settings", new List<string> { ".json" });

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            _settings.ExportSettings(file.Path);
        }
    }

    private async void OnImportSettingsClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            _settings.ImportSettings(file.Path);
            _isInitializing = true;
            LoadCurrentSettings();
            _isInitializing = false;
        }
    }

    private async void OnOpenLogsClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var crashLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log");
            var traceLogPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_trace.log");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== DISWIN DIAGNOSTICS ({UpdateService.CurrentVersion}) ===");
            sb.AppendLine($"Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            if (System.IO.File.Exists(traceLogPath))
            {
                sb.AppendLine("--- TRACE LOG ---");
                var lines = System.IO.File.ReadAllLines(traceLogPath);
                foreach (var line in lines.TakeLast(120))
                    sb.AppendLine(line);
                sb.AppendLine();
            }

            if (System.IO.File.Exists(crashLogPath))
            {
                sb.AppendLine("--- CRASH LOG ---");
                sb.AppendLine(System.IO.File.ReadAllText(crashLogPath));
            }
            else
            {
                sb.AppendLine("Aucune erreur critique enregistrée.");
            }

            var logTextBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 11,
                MinHeight = 320,
                MaxHeight = 420
            };

            var dialog = new ContentDialog
            {
                Title = "Journaux de diagnostic",
                Content = new ScrollViewer
                {
                    Content = logTextBox,
                    MaxHeight = 440,
                    MinWidth = 540
                },
                PrimaryButtonText = "Copier tout",
                SecondaryButtonText = "Vider les journaux",
                CloseButtonText = "Fermer",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var pkg = new DataPackage();
                pkg.SetText(logTextBox.Text);
                Clipboard.SetContent(pkg);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                try { if (System.IO.File.Exists(crashLogPath)) System.IO.File.Delete(crashLogPath); } catch { }
                try { if (System.IO.File.Exists(traceLogPath)) System.IO.File.Delete(traceLogPath); } catch { }
            }
        }
        catch { }
    }

    private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.BackgroundOpacity = e.NewValue;
        _settings.Save();
    }

    private void OnBlurChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.BackgroundBlurRadius = e.NewValue;
        _settings.Save();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (ThemeCombo.SelectedItem is ComboBoxItem item)
        {
            _settings.Current.ThemeMode = (string)item.Tag;
            _settings.Save();

            var theme = _settings.Current.ThemeMode switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            if (Content is FrameworkElement root) root.RequestedTheme = theme;
            if (App.MainAppWindow?.Content is FrameworkElement mainRoot) mainRoot.RequestedTheme = theme;
        }
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (LanguageCombo.SelectedItem is ComboBoxItem item)
        {
            var lang = (string)item.Tag;
            _settings.Current.Language = lang;
            _settings.Save();

            try
            {
                if (lang != "Default")
                {
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(lang);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
                }
                else
                {
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;
                }
            }
            catch { }
        }
    }

    private void OnCompactModeToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.Current.CompactMode = CompactModeToggle.IsOn;
        _settings.Save();
    }

    private async void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Se déconnecter ?",
            Content = "Vous devrez ressaisir vos identifiants pour vous reconnecter.",
            PrimaryButtonText = "Se déconnecter",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _settings.ClearToken();
            var loginWindow = new LoginWindow();
            loginWindow.Activate();
            if (App.MainAppWindow is MainWindow mw)
            {
                mw.ExitApplication();
            }
            App.MainAppWindow = loginWindow;
            Close();
        }
    }

    private void OnQuitDiswinClicked(object sender, RoutedEventArgs e)
    {
        Close();
        if (App.MainAppWindow is MainWindow mw)
        {
            mw.ExitApplication();
        }
        else
        {
            Application.Current.Exit();
        }
    }
}
