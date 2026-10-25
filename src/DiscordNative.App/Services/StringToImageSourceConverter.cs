using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DiscordNative.App.Services;

public sealed class StringToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    // Pour les GIFs et images animées (klipy, tenor, discord cdn .gif), AutoPlay = true
                    bool isAnimated = url.Contains(".gif", StringComparison.OrdinalIgnoreCase) ||
                                      url.Contains("animated=true", StringComparison.OrdinalIgnoreCase) ||
                                      url.Contains("klipy.com", StringComparison.OrdinalIgnoreCase);

                    return new BitmapImage(uri) { AutoPlay = isAnimated };
                }
                else if (Uri.TryCreate($"ms-appx:///{url.TrimStart('/', '\\')}", UriKind.Absolute, out var localUri))
                {
                    return new BitmapImage(localUri) { AutoPlay = false };
                }
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
