using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace DiscordNative.App.Services;

/// <summary>
/// Service de lecture audio pour le Soundboard Discord avec mise en cache locale des fichiers sons.
/// </summary>
public static class SoundboardPlayerService
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string _cacheDir = Path.Combine(Path.GetTempPath(), "DiscordNative", "SoundboardCache");

    public static async Task PlaySoundAsync(string soundId, double volume = 1.0)
    {
        try
        {
            Directory.CreateDirectory(_cacheDir);
            var localPath = Path.Combine(_cacheDir, $"{soundId}.mp3");

            if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
            {
                var url = $"https://cdn.discordapp.com/soundboard-sounds/{soundId}";
                var bytes = await _httpClient.GetByteArrayAsync(url);
                if (bytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(localPath, bytes);
                }
            }

            if (File.Exists(localPath))
            {
                Protocol.App.RunOnUiThread(() =>
                {
                    try
                    {
                        var player = new MediaPlayer
                        {
                            Volume = Math.Clamp(volume, 0.05, 1.0)
                        };
                        player.Source = MediaSource.CreateFromUri(new Uri(localPath));
                        player.MediaEnded += (sender, _) =>
                        {
                            try { sender.Dispose(); } catch { }
                        };
                        player.Play();
                    }
                    catch { }
                });
            }
        }
        catch { }
    }
}
