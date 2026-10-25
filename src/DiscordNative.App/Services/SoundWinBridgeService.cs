using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DiscordNative.Protocol;

namespace DiscordNative.App.Services;

/// <summary>
/// Service client IPC pour l'écosystème Jay reliant Diswin à SoundWin.
/// Se connecte au pipe nommé \\.\pipe\Jay_SoundWin_Ecosystem pour détecter en temps réel
/// si SoundWin est lancé, afficher le lecteur SoundCloud dans la barre latérale,
/// piloter la lecture à distance et partager le titre dans le chat Discord.
/// </summary>
public sealed partial class SoundWinBridgeService : ObservableObject, IDisposable
{
    public const string PipeName = "Jay_SoundWin_Ecosystem";

    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private long _trackId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private string _artworkUrl = string.Empty;

    [ObservableProperty]
    private string _permalinkUrl = string.Empty;

    [ObservableProperty]
    private long _durationMs;

    [ObservableProperty]
    private long _positionMs;

    [ObservableProperty]
    private double _progress; // 0.0 à 1.0

    [ObservableProperty]
    private string _positionFormatted = "0:00";

    [ObservableProperty]
    private string _durationFormatted = "0:00";

    public bool HasTrack => !string.IsNullOrEmpty(Title);

    public SoundWinBridgeService()
    {
        StartConnectionLoop();
    }

    private void StartConnectionLoop()
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    try
                    {
                        var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        await pipe.ConnectAsync(800, _cts.Token).ConfigureAwait(false);

                        _pipe = pipe;
                        _writer = new StreamWriter(_pipe, new UTF8Encoding(false), leaveOpen: true)
                        {
                            AutoFlush = true
                        };

                        DiscordNative.Protocol.App.RunOnUiThread(() =>
                        {
                            IsConnected = true;
                        });

                        // Demande l'état initial
                        await SendCommandAsync("get_state").ConfigureAwait(false);

                        // Boucle de lecture des événements reçus de SoundWin
                        using var reader = new StreamReader(_pipe, Encoding.UTF8, leaveOpen: true);
                        while (!_cts.Token.IsCancellationRequested && _pipe.IsConnected)
                        {
                            var line = await reader.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                            if (line == null) break;

                            ProcessIncomingMessage(line);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // SoundWin n'est pas lancé ou a été fermé
                    }
                    finally
                    {
                        CleanupConnection();
                    }
                }

                await Task.Delay(2000, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);
    }

    private void CleanupConnection()
    {
        try
        {
            _writer?.Dispose();
            _writer = null;

            _pipe?.Dispose();
            _pipe = null;
        }
        catch { }

        DiscordNative.Protocol.App.RunOnUiThread(() =>
        {
            IsConnected = false;
            IsPlaying = false;
        });
    }

    private void ProcessIncomingMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("event", out var evEl)) return;

            var ev = evEl.GetString();
            if (ev == "state")
            {
                var isPlaying = root.TryGetProperty("isPlaying", out var pEl) && pEl.GetBoolean();
                var trackId = root.TryGetProperty("trackId", out var tEl) ? tEl.GetInt64() : 0;
                var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? string.Empty : string.Empty;
                var artist = root.TryGetProperty("artist", out var artEl) ? artEl.GetString() ?? string.Empty : string.Empty;
                var artwork = root.TryGetProperty("artworkUrl", out var awEl) ? awEl.GetString() ?? string.Empty : string.Empty;
                var permalink = root.TryGetProperty("permalinkUrl", out var plEl) ? plEl.GetString() ?? string.Empty : string.Empty;
                var durationMs = root.TryGetProperty("durationMs", out var durEl) ? durEl.GetInt64() : 0;
                var posMs = root.TryGetProperty("positionMs", out var posEl) ? posEl.GetInt64() : 0;
                var progress = root.TryGetProperty("progress", out var prEl) ? prEl.GetDouble() : 0.0;

                DiscordNative.Protocol.App.RunOnUiThread(() =>
                {
                    IsPlaying = isPlaying;
                    TrackId = trackId;
                    Title = title;
                    Artist = artist;
                    ArtworkUrl = artwork;
                    PermalinkUrl = permalink;
                    DurationMs = durationMs;
                    PositionMs = posMs;
                    Progress = Math.Clamp(progress, 0.0, 1.0);

                    PositionFormatted = TimeSpan.FromMilliseconds(posMs).ToString(@"m\:ss");
                    DurationFormatted = TimeSpan.FromMilliseconds(durationMs).ToString(@"m\:ss");
                    OnPropertyChanged(nameof(HasTrack));
                });
            }
            else if (ev == "progress")
            {
                var posMs = root.TryGetProperty("positionMs", out var posEl) ? posEl.GetInt64() : 0;
                var progress = root.TryGetProperty("progress", out var prEl) ? prEl.GetDouble() : 0.0;

                DiscordNative.Protocol.App.RunOnUiThread(() =>
                {
                    PositionMs = posMs;
                    Progress = Math.Clamp(progress, 0.0, 1.0);
                    PositionFormatted = TimeSpan.FromMilliseconds(posMs).ToString(@"m\:ss");
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundWinBridgeService] Erreur parsing message : {ex.Message}");
        }
    }

    public async Task SendCommandAsync(string command)
    {
        if (_pipe == null || !_pipe.IsConnected || _writer == null) return;

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_writer != null && _pipe.IsConnected)
            {
                var json = JsonSerializer.Serialize(new { command });
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SoundWinBridgeService] Erreur envoi commande '{command}': {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public Task TogglePlayPauseAsync() => SendCommandAsync("toggle_play");
    public Task NextAsync() => SendCommandAsync("next");
    public Task PreviousAsync() => SendCommandAsync("prev");
    public Task FocusSoundWinAsync() => SendCommandAsync("focus");

    public void Dispose()
    {
        _cts.Cancel();
        CleanupConnection();
        _sendLock.Dispose();
    }
}
