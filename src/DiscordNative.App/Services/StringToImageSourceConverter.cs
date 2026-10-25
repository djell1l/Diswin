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
                    var bi = new BitmapImage(uri) { AutoPlay = true };
                    bi.ImageOpened += (s, e) =>
                    {
                        try
                        {
                            if (bi.IsAnimatedBitmap)
                            {
                                bi.Play();
                            }
                        }
                        catch { }
                    };
                    return bi;
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
