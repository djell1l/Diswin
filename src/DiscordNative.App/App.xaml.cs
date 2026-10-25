using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using DiscordNative.App.Services;
using DiscordNative.App.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace DiscordNative.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\DiscordNative_Diswin_SingleInstance_Mutex";
    private const string SingleInstancePipeName = "DiscordNative_Diswin_SingleInstance_Pipe";
    private static Mutex? _singleInstanceMutex;
    private static CancellationTokenSource? _pipeCts;

    public static Window? MainAppWindow { get; set; }
    public static SettingsService Settings { get; } = new();

    private DispatcherQueue? _dispatcherQueue;

    public App()
    {
        bool isFirstInstance = false;
        try
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out isFirstInstance);
        }
        catch
        {
            isFirstInstance = true;
        }

        if (!isFirstInstance)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out);
                pipeClient.Connect(800);
                pipeClient.WriteByte(1);
                pipeClient.Flush();
            }
            catch { }

            Environment.Exit(0);
            return;
        }

        StartSingleInstancePipeServer();

        var theme = Settings.Current.ThemeMode;
        if (theme == "Light") RequestedTheme = ApplicationTheme.Light;
        else if (theme == "Dark") RequestedTheme = ApplicationTheme.Dark;

        var lang = Settings.Current.Language;
        if (!string.IsNullOrEmpty(lang) && lang != "Default")
        {
            try
            {
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = lang;
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(lang);
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(lang);
            }
            catch { }
        }

        InitializeComponent();
        this.UnhandledException += OnAppUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
#if DEBUG
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_firstchance.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}] {e.Exception.GetType().Name}: {e.Exception.Message}\n{Environment.StackTrace}\n\n");
            }
            catch { }
        };
#endif
    }

    private void StartSingleInstancePipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        var ct = _pipeCts.Token;
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        SingleInstancePipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(ct);
                    _ = server.ReadByte();

                    Protocol.App.RunOnUiThread(() =>
                    {
                        try
                        {
                            if (MainAppWindow is MainWindow mw)
                            {
                                mw.ShowAndActivate();
                            }
                            else if (MainAppWindow != null)
                            {
                                MainAppWindow.Activate();
                            }
                        }
                        catch { }
                    });
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(500, ct); }
            }
        }, ct);
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash("UnhandledException (App)", e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogCrash("UnhandledException (AppDomain)", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    internal static void Trace(string msg)
    {
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_trace.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
        }
        catch { }
    }

    internal static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DiscordNative_crash.log");
            System.IO.File.AppendAllText(path,
                $"[{DateTime.Now}] {source}: {ex}\n\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            Protocol.App.RunOnUiThread = action => _dispatcherQueue!.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex) { LogCrash("RunOnUiThread", ex); }
            });

            var hasToken = !string.IsNullOrEmpty(Settings.Current.SavedToken);
            Trace($"OnLaunched hasToken={hasToken}");

            MainAppWindow = hasToken ? new MainWindow(Settings.Current.SavedToken!) : new LoginWindow();
            MainAppWindow.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched", ex);
            throw;
        }
    }
}
