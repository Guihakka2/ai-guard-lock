using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AiGuardLock.Core;

namespace AiGuardLock;

internal sealed class SecondaryCoverWindow : Window
{
    private bool _allowClose;

    internal SecondaryCoverWindow(OverlayWindow owner)
    {
        Owner = owner;
        Title = UiText.Get("AI Guard Lock — 画面カバー", "AI Guard Lock — Screen cover");
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0x70, 0x0A, 0x14, 0x22));
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xFC, 0x12, 0x25, 0x38)),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(24), Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = UiText.Get("画面カバー中\n主画面で本人確認すると、すべてのカバーを解除します。",
                    "Screen covered\nVerify on the primary screen to release all covers."),
                Foreground = Brushes.White, FontSize = 18, TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            e.Handled = true;
            owner.Activate();
        };
    }

    internal void Release()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Owner?.Activate();
        }
        base.OnClosing(e);
    }
}
