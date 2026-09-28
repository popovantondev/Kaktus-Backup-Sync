using System.Windows.Media;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AntonsBackupManager.App;

public static class Brand
{
    public const string Name = "Kaktus Backup & Sync";
    public const string ExecutableName = "KaktusBackupSync.exe";
    public static Brush Surface { get; } = FrozenBrush(243, 246, 239);
    public static Brush Ink { get; } = FrozenBrush(23, 59, 49);
    public static ImageSource Mascot { get; } = LoadMascot();

    private static ImageSource LoadMascot()
    {
        var resource = Application.GetResourceStream(new Uri("/KaktusBackupSync;component/Assets/KaktusMascot.png", UriKind.Relative))
            ?? throw new InvalidOperationException("The Kaktus mascot resource is missing.");
        using var stream = resource.Stream;
        var bitmap = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
