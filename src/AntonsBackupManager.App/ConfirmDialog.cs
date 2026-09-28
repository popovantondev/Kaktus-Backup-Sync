using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AntonsBackupManager.App;

public sealed class ConfirmDialog : Window
{
    public ConfirmDialog(Window owner, string title, string message)
    {
        Owner = owner;
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brand.Surface;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold,
            Foreground = Brand.Ink, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new ScrollViewer { MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0,16,0,24),
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, LineHeight = 23 } });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = UiText.Get("Cancel"), IsCancel = true, IsDefault = true, Style = (Style)owner.FindResource(typeof(Button)), Margin = new Thickness(0,0,12,0) };
        var confirm = new Button { Content = UiText.Get("Continue"), Style = (Style)owner.FindResource("Primary") };
        cancel.Click += (_, _) => DialogResult = false;
        confirm.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        content.Children.Add(buttons);
        Content = content;
    }

    public static bool Ask(Window owner, string title, string message) => new ConfirmDialog(owner, title, message).ShowDialog() == true;
}
