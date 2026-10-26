using System;
using System.Collections.Generic;
using System.IO;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace DiscordNative.App.Services;

public enum DiscordSfx
{
    Join,
    Disconnect,
    Mute,
    Unmute,
    Deafen,
    Undeafen
}

/// <summary>
/// Service de lecture des effets sonores officiels de Discord (Connexion vocale, déconnexion, micro, casque).
/// </summary>
public static class AudioSfxService
{
    private static readonly Dictionary<DiscordSfx, string> _fileNames = new()
    {
        [DiscordSfx.Join] = "join.mp3",
        [DiscordSfx.Disconnect] = "disconnect.mp3",
        [DiscordSfx.Mute] = "mute.mp3",
        [DiscordSfx.Unmute] = "unmute.mp3",
        [DiscordSfx.Deafen] = "deafen.mp3",
        [DiscordSfx.Undeafen] = "undeafen.mp3",
    };

    public static void Play(DiscordSfx sfx)
    {
        try
        {
            if (!_fileNames.TryGetValue(sfx, out var fileName)) return;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(baseDir, "Assets", "Sounds", fileName);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine("Assets", "Sounds", fileName);
                if (!File.Exists(fullPath)) return;
            }

            var player = new MediaPlayer
            {
                Volume = 0.80
            };
            player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(fullPath)));
            player.MediaEnded += (sender, _) =>
            {
                try { sender.Dispose(); } catch { }
            };
            player.Play();
        }
        catch { }
    }

    /// <summary>
    /// Joue l'intro personnalisée Diswin en assemblant les deux fichiers audio (« Diswin! » + jingle)
    /// comme une seule séquence d'introduction au démarrage ou lors de l'easter egg du bouton Diswin.
    /// </summary>
    public static void PlayDiswinIntro()
    {
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                PlayCustomFile("diswin_voice.mp3", 0.90);
                await System.Threading.Tasks.Task.Delay(250);
                PlayCustomFile("diswin_chime.mp3", 0.85);
            }
            catch { }
        });
    }

    private static void PlayCustomFile(string fileName, double volume)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(baseDir, "Assets", "Sounds", fileName);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine("Assets", "Sounds", fileName);
                if (!File.Exists(fullPath)) return;
            }

            var player = new MediaPlayer
            {
                Volume = volume
            };
            player.Source = MediaSource.CreateFromUri(new Uri(Path.GetFullPath(fullPath)));
            player.MediaEnded += (sender, _) =>
            {
                try { sender.Dispose(); } catch { }
            };
            player.Play();
        }
        catch { }
    }
}
