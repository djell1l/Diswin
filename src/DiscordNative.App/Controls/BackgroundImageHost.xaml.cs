using System;
using System.IO;
using System.Numerics;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DiscordNative.App.Controls;

/// <summary>
/// Hôte du fond d'écran personnalisé de l'appli, inspiré du réglage "Custom background"
/// de Files : image/gif utilisateur + voile semi-transparent + flou gaussien via Win2D.
/// Optimisé pour CPU/GPU : mise en cache des états et aucun abonnement redondant à SizeChanged.
/// </summary>
public sealed partial class BackgroundImageHost : UserControl
{
    private SpriteVisual? _blurVisual;
    private CompositionEffectBrush? _blurBrush;
    private string? _appliedPath;
    private double _appliedOpacity = -1;
    private double _appliedBlur = -1;

    public BackgroundImageHost()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyFromSettings();
        SizeChanged += OnHostSizeChanged;
        App.Settings.SettingsChanged += ApplyFromSettings;
    }

    private void OnHostSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        if (_blurVisual is not null)
        {
            _blurVisual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
        }
    }

    public void ApplyFromSettings()
    {
        var settings = App.Settings.Current;

        if (string.IsNullOrEmpty(settings.BackgroundImagePath) || !File.Exists(settings.BackgroundImagePath))
        {
            if (_appliedPath != null)
            {
                BackgroundImage.Source = null;
                OverlayGrid.Opacity = 0;
                RemoveBlur();
                _appliedPath = null;
                _appliedOpacity = -1;
                _appliedBlur = -1;
            }
            return;
        }

        if (_appliedPath != settings.BackgroundImagePath)
        {
            _appliedPath = settings.BackgroundImagePath;
            if (Uri.TryCreate(settings.BackgroundImagePath, UriKind.Absolute, out var uri))
            {
                var bi = new BitmapImage(uri) { AutoPlay = true };
                bi.ImageOpened += (s, e) =>
                {
                    try
                    {
                        if (bi.IsAnimatedBitmap)
                            bi.Play();
                    }
                    catch { }
                };
                BackgroundImage.Source = bi;
            }
        }

        if (Math.Abs(_appliedOpacity - settings.BackgroundOpacity) > 0.005)
        {
            _appliedOpacity = settings.BackgroundOpacity;
            OverlayGrid.Opacity = settings.BackgroundOpacity;
        }

        if (Math.Abs(_appliedBlur - settings.BackgroundBlurRadius) > 0.005)
        {
            _appliedBlur = settings.BackgroundBlurRadius;
            ApplyBlur(settings.BackgroundBlurRadius);
        }
    }

    private void ApplyBlur(double radius)
    {
        RemoveBlur();
        if (radius <= 0.5) return;

        try
        {
            var hostVisual = ElementCompositionPreview.GetElementVisual(BackgroundImage);
            var compositor = hostVisual.Compositor;

            var graphicsEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = (float)radius,
                Source = new CompositionEffectSourceParameter("Source"),
                BorderMode = EffectBorderMode.Hard,
            };

            var effectFactory = compositor.CreateEffectFactory(graphicsEffect);
            _blurBrush = effectFactory.CreateBrush();
            _blurBrush.SetSourceParameter("Source", compositor.CreateBackdropBrush());

            _blurVisual = compositor.CreateSpriteVisual();
            _blurVisual.Brush = _blurBrush;
            _blurVisual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);

            ElementCompositionPreview.SetElementChildVisual(OverlayGrid, _blurVisual);
        }
        catch { }
    }

    private void RemoveBlur()
    {
        ElementCompositionPreview.SetElementChildVisual(OverlayGrid, null);
        _blurBrush?.Dispose();
        _blurBrush = null;
        _blurVisual?.Dispose();
        _blurVisual = null;
    }
}
