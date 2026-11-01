using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using AiGuardLock.Core;

namespace AiGuardLock.CrossPlatform;

internal static class Ui
{
    public static IBrush Teal => Brush.Parse("#087E79");
    public static TextBlock Text(string text, double size = 14, string color = "#152C40") =>
        new() { Text = text, FontSize = size, Foreground = Brush.Parse(color), TextWrapping = TextWrapping.Wrap };
    public static Button Button(string label) => new()
    {
        Content = label, HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(14, 12)
    };
    public static TextBox Secret() => new() { PasswordChar = '●', MaxLength = 128, FontSize = 20, Padding = new Thickness(12, 10) };
    public static WindowIcon Icon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://AiGuardLock.Portable/Assets/AppIcon.png"));
        return new WindowIcon(stream);
    }
}

public sealed class SetupWindow : Window
{
    internal readonly TextBox Pin = Ui.Secret();
    internal readonly TextBox Confirmation = Ui.Secret();
    internal readonly CheckBox Acknowledge = new() { Content = "主画面のみ対応で、OSのロックではないことを確認しました。" };
    internal readonly ComboBox Duration = new()
    {
        ItemsSource = new[] { "しない（PINで解除）", "2分（動作確認用）", "10分", "60分" },
        SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch
    };
    internal readonly TextBlock Status = Ui.Text("初回は2分の自動解除で動作を確認してください。", 13, "#52697A");
    internal readonly Button Start = Ui.Button("半透明カバーを開始");
    internal CoverWindow? Cover { get; private set; }
    private bool _closing;

    public SetupWindow()
    {
        Title = "AI Guard Lock — macOS / Linux";
        Width = 620; Height = 800; MinWidth = 420; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#F3F6F8");
        Icon = Ui.Icon();
        var body = new StackPanel { Margin = new Thickness(32, 26), Spacing = 12 };
        body.Children.Add(Ui.Text("PASSWORD SCREEN COVER", 12, "#087E79"));
        body.Children.Add(Ui.Text("AI Guard Lock", 32));
        body.Children.Add(Ui.Text("背景を半透明で表示し、アプリ専用PINで作業に戻る。", 14, "#52697A"));
        body.Children.Add(new Border
        {
            Background = Brush.Parse("#FFF3DD"), Padding = new Thickness(16), CornerRadius = new CornerRadius(8),
            Child = Ui.Text("macOS・Linux版はアプリ専用PIN／パスワードを使います。Touch ID・OSログイン認証は未対応です。画面切替やプロセス終了は防げません。", 13)
        });
        body.Children.Add(Ui.Text("PIN（半角数字4〜12桁）またはパスワード（8〜128文字）"));
        body.Children.Add(Pin);
        body.Children.Add(Ui.Text("もう一度入力"));
        body.Children.Add(Confirmation);
        body.Children.Add(Ui.Text("今回のカバー表示中だけ有効です。ファイルには保存しません。", 12, "#52697A"));
        body.Children.Add(Ui.Text("自動解除"));
        body.Children.Add(Duration);
        body.Children.Add(Acknowledge);
        body.Children.Add(Ui.Text("背景のCLI・API処理を想定しています。画面操作を行うAIとは併用できません。OSのスリープ設定は変更しません。", 12, "#52697A"));
        body.Children.Add(Status);
        Start.Background = Ui.Teal; Start.Foreground = Brushes.White;
        Start.Click += (_, _) => StartCover();
        body.Children.Add(Start);
        var exit = Ui.Button("終了");
        exit.Click += (_, _) => Close();
        body.Children.Add(exit);
        Content = new ScrollViewer { Content = body };
        Opened += (_, _) => Pin.Focus();
    }

    internal void StartCover()
    {
        if (Cover is not null) return;
        if (Acknowledge.IsChecked != true) { Status.Text = "注意事項を確認してチェックを入れてください。"; return; }
        if (!string.Equals(Pin.Text, Confirmation.Text, StringComparison.Ordinal))
        {
            Status.Text = "確認用の入力が一致していません。";
            Confirmation.Text = string.Empty;
            return;
        }
        PasswordCredential credential;
        try { credential = new PasswordCredential((Pin.Text ?? string.Empty).AsSpan()); }
        catch (ArgumentException) { Status.Text = "数字PINは4〜12桁、パスワードは空白だけを避けて8〜128文字です。"; return; }
        var minutes = Duration.SelectedIndex switch { 1 => 2, 2 => 10, 3 => 60, _ => 0 };
        try
        {
            var cover = new CoverWindow(new CoverSession(credential, minutes == 0 ? null : TimeSpan.FromMinutes(minutes)));
            Cover = cover;
            cover.Closed += (_, _) =>
            {
                Cover = null;
                if (_closing) return;
                Show(); Activate();
                Status.Text = cover.Result switch
                {
                    CoverResult.Unlocked => "PINで解除しました。再開時には新しいPINを設定してください。",
                    CoverResult.TimedOut => "指定した時間になったため解除しました。",
                    CoverResult.DisplayChanged => "画面構成が変わったため解除しました。",
                    CoverResult.TransparencyUnavailable => "この画面環境では半透明表示を利用できません。Linuxでは合成処理が有効なX11セッションを確認してください。",
                    _ => "カバーを終了しました。"
                };
                Pin.Focus();
            };
            cover.Show();
            if (cover.IsVisible) Hide();
        }
        catch (Exception)
        {
            Cover?.Finish(CoverResult.Closed);
            Cover = null;
            credential.Dispose();
            Show();
            Status.Text = "カバーを開始できませんでした。設定画面に戻りました。";
        }
        finally { Pin.Text = string.Empty; Confirmation.Text = string.Empty; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        Cover?.Finish(CoverResult.Closed);
        Pin.Text = string.Empty; Confirmation.Text = string.Empty;
        base.OnClosed(e);
    }
}

internal sealed class CoverWindow : Window
{
    private readonly CoverSession _session;
    private Screens? _screens;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _countdown = Ui.Text("", 13, "#B5C6D6");
    internal readonly TextBox Pin = Ui.Secret();
    internal readonly Button Unlock = Ui.Button("解除");
    internal readonly TextBlock Feedback = Ui.Text("開始時に設定したPIN・パスワードを入力してください。", 13, "#FFD997");
    internal CoverResult Result => _session.Result;

    public CoverWindow(CoverSession session)
    {
        _session = session;
        Title = "AI Guard Lock — 半透明カバー";
        Icon = Ui.Icon();
        SystemDecorations = SystemDecorations.None;
        CanResize = false; Topmost = true; ShowInTaskbar = true;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brush.Parse("#700A1422");
        WindowStartupLocation = WindowStartupLocation.Manual;
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(Ui.Text("AI GUARD LOCK", 12, "#81DBD2"));
        body.Children.Add(Ui.Text("PINで作業に戻る", 28, "#FFFFFF"));
        body.Children.Add(Ui.Text("背後の画面はそのままです。本人確認でカバーを解除します。", 14, "#B5C6D6"));
        body.Children.Add(Pin);
        body.Children.Add(Feedback);
        Unlock.Background = Brush.Parse("#81DBD2"); Unlock.Foreground = Brush.Parse("#10283A");
        Unlock.Click += (_, _) => Submit();
        body.Children.Add(Unlock);
        body.Children.Add(_countdown);
        body.Children.Add(Ui.Text("OSのロックではありません。復旧時はmacOSの強制終了、Linuxのシステムモニターからこのアプリを終了できます。", 12, "#B5C6D6"));
        Content = new Grid
        {
            Margin = new Thickness(24),
            Children =
            {
                new ScrollViewer
                {
                    MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    Content = new Border { Background = Brush.Parse("#FC122538"), CornerRadius = new CornerRadius(18), Padding = new Thickness(30), Child = body }
                }
            }
        };
        Opened += OnOpened;
        _timer.Tick += OnTick;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Submit(); }
            if (e.Key == Key.Escape) { e.Handled = true; Feedback.Text = "解除するにはPINを入力してください。"; Pin.Focus(); }
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary ?? Screens.ScreenFromWindow(this);
        if (screen is null) { Finish(CoverResult.DisplayChanged); return; }
        Position = screen.Bounds.Position;
        Width = screen.Bounds.Width / screen.Scaling;
        Height = screen.Bounds.Height / screen.Scaling;
        _screens = Screens;
        _screens.Changed += OnScreensChanged;
        _timer.Start();
        Pin.Focus();
        // Let the native compositor apply the requested transparency first.
        Dispatcher.UIThread.Post(() =>
        {
            if (Result == CoverResult.Active && ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
                Finish(CoverResult.TransparencyUnavailable);
        }, DispatcherPriority.Background);
        OnTick(null, EventArgs.Empty);
    }

    internal void Submit()
    {
        var value = Pin.Text ?? string.Empty;
        Pin.Text = string.Empty;
        if (_session.TryUnlock(value.AsSpan())) { Close(); return; }
        if (Result != CoverResult.Active) { Close(); return; }
        Feedback.Text = "PIN・パスワードが違うか、再試行の待機中です。1秒待ってから入力してください。";
        Unlock.IsEnabled = _session.CanRetry;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _session.Tick();
        if (Result != CoverResult.Active) { Close(); return; }
        Unlock.IsEnabled = _session.CanRetry;
        _countdown.Text = _session.Remaining is { } remaining
            ? $"自動解除まで {Math.Ceiling(remaining.TotalSeconds):0}秒 / 主画面のみ"
            : "自動解除なし / 主画面のみ";
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Finish(CoverResult.DisplayChanged);
    internal void Finish(CoverResult result) { _session.Finish(result); Close(); }
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Result == CoverResult.Active) { e.Cancel = true; Feedback.Text = "解除するにはPINを入力してください。"; }
        base.OnClosing(e);
    }
    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop(); _timer.Tick -= OnTick;
        if (_screens is not null) _screens.Changed -= OnScreensChanged;
        Pin.Text = string.Empty;
        _session.Dispose();
        base.OnClosed(e);
    }
}
