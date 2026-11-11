using Avalonia;
using Avalonia.Automation;
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
    internal readonly CheckBox Acknowledge = new() { Content = Ui.Text(UiText.Get("選択した画面のみを覆い、OSのロックではないことを確認しました。", "I understand that this covers only the selected screen and is not an OS lock.")) };
    internal readonly ComboBox DisplayChoice = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    internal readonly ComboBox Duration = new()
    {
        ItemsSource = new[] { UiText.Get("しない（PINで解除）", "Never (unlock with PIN)"), UiText.Get("2分（動作確認用）", "2 minutes (test first)"), UiText.Get("10分", "10 minutes"), UiText.Get("60分", "60 minutes") },
        SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch
    };
    internal readonly TextBlock Status = Ui.Text(UiText.Get("初回は2分の自動解除で動作を確認してください。", "For your first use, test with the 2-minute automatic unlock."), 13, "#52697A");
    internal readonly Button Start = Ui.Button(UiText.Get("半透明カバーを開始", "Start translucent cover"));
    internal CoverWindow? Cover { get; private set; }
    private bool _closing;

    public SetupWindow()
    {
        AutomationProperties.SetName(Pin, UiText.Get("PIN・パスワード", "PIN or password"));
        AutomationProperties.SetName(Confirmation, UiText.Get("確認用のPIN・パスワード", "Confirm PIN or password"));
        AutomationProperties.SetName(Duration, UiText.Get("自動解除", "Automatic unlock"));
        AutomationProperties.SetName(DisplayChoice, UiText.Get("カバーする画面", "Screen to cover"));
        Title = "AI Guard Lock — macOS / Linux";
        Width = 620; Height = 800; MinWidth = 420; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush.Parse("#F3F6F8");
        Icon = Ui.Icon();
        var body = new StackPanel { Margin = new Thickness(32, 26), Spacing = 12 };
        body.Children.Add(Ui.Text("PASSWORD SCREEN COVER", 12, "#087E79"));
        body.Children.Add(Ui.Text("AI Guard Lock", 32));
        body.Children.Add(Ui.Text(UiText.Get("背景を半透明で表示し、アプリ専用PINで作業に戻る。", "Keep your work visible through a translucent cover. Return with an app PIN."), 14, "#52697A"));
        body.Children.Add(new Border
        {
            Background = Brush.Parse("#FFF3DD"), Padding = new Thickness(16), CornerRadius = new CornerRadius(8),
            Child = Ui.Text(UiText.Get("macOS・Linux版はアプリ専用PIN／パスワードを使います。Touch ID・OSログイン認証は未対応です。画面切替やプロセス終了は防げません。", "The macOS and Linux preview uses an app PIN or password. Touch ID and OS sign-in are not supported. It cannot prevent switching screens or ending the process."), 13)
        });
        body.Children.Add(Ui.Text(UiText.Get("PIN（半角数字4〜12桁）またはパスワード（8〜128文字）", "PIN (4–12 digits) or password (8–128 characters)")));
        body.Children.Add(Pin);
        body.Children.Add(Ui.Text(UiText.Get("もう一度入力", "Confirm PIN or password")));
        body.Children.Add(Confirmation);
        body.Children.Add(Ui.Text(UiText.Get("今回のカバー表示中だけ有効です。ファイルには保存しません。", "Valid only for this cover session. Never saved to a file."), 12, "#52697A"));
        body.Children.Add(Ui.Text(UiText.Get("自動解除", "Automatic unlock")));
        body.Children.Add(Duration);
        body.Children.Add(Ui.Text(UiText.Get("カバーする画面", "Screen to cover")));
        body.Children.Add(DisplayChoice);
        body.Children.Add(Acknowledge);
        body.Children.Add(Ui.Text(UiText.Get("背景のCLI・API処理を想定しています。画面操作を行うAIとは併用できません。OSのスリープ設定は変更しません。", "Designed for background CLI and API tasks. Do not use with AI that controls the screen. OS sleep settings are unchanged."), 12, "#52697A"));
        body.Children.Add(Status);
        Start.Background = Ui.Teal; Start.Foreground = Brushes.White;
        Start.Click += (_, _) => StartCover();
        body.Children.Add(Start);
        var exit = Ui.Button(UiText.Get("終了", "Exit"));
        exit.Click += (_, _) => Close();
        body.Children.Add(exit);
        Content = new ScrollViewer { Content = body };
        DisplayChoice.Items.Add(new ComboBoxItem { Content = UiText.Get("主画面のみ", "Primary screen only") });
        DisplayChoice.SelectedIndex = 0;
        DisplayChoice.DropDownOpened += (_, _) => RefreshDisplays();
        Opened += (_, _) => { RefreshDisplays(); Pin.Focus(); };
    }

    private void RefreshDisplays()
    {
        var selected = (DisplayChoice.SelectedItem as ComboBoxItem)?.Tag as Screen;
        var primary = DisplayChoice.SelectedIndex == 0;
        try
        {
            var screens = Screens.All.ToArray();
            while (DisplayChoice.Items.Count > 1) DisplayChoice.Items.RemoveAt(1);
            for (var i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                var label = UiText.Get($"モニター {i + 1}", $"Monitor {i + 1}");
                if (screen.IsPrimary) label += UiText.Get("（主画面）", " (primary)");
                var item = new ComboBoxItem
                {
                    Content = $"{label} — {screen.Bounds.Width} × {screen.Bounds.Height} ({screen.Bounds.X}, {screen.Bounds.Y})",
                    Tag = screen
                };
                DisplayChoice.Items.Add(item);
                if (screen.Equals(selected)) DisplayChoice.SelectedItem = item;
            }
            if (primary) DisplayChoice.SelectedIndex = 0;
        }
        catch (Exception)
        {
            Status.Text = UiText.Get("画面一覧を取得できません。もう一度開いてください。", "Could not list monitors. Open the list to retry.");
        }
    }

    internal void StartCover()
    {
        if (Cover is not null) return;
        if (DisplayChoice.SelectedIndex < 0)
        {
            Status.Text = UiText.Get("カバーする画面を選択してください。", "Select a monitor to cover.");
            return;
        }
        if (Acknowledge.IsChecked != true) { Status.Text = UiText.Get("注意事項を確認してチェックを入れてください。", "Read the limitations and check the acknowledgment box."); return; }
        if (!string.Equals(Pin.Text, Confirmation.Text, StringComparison.Ordinal))
        {
            Status.Text = UiText.Get("確認用の入力が一致していません。", "The confirmation does not match.");
            Confirmation.Text = string.Empty;
            return;
        }
        PasswordCredential credential;
        try { credential = new PasswordCredential((Pin.Text ?? string.Empty).AsSpan()); }
        catch (ArgumentException) { Status.Text = UiText.Get("数字PINは4〜12桁、パスワードは空白だけを避けて8〜128文字です。", "Use a 4–12 digit PIN or an 8–128 character password that is not only whitespace."); return; }
        var minutes = Duration.SelectedIndex switch { 1 => 2, 2 => 10, 3 => 60, _ => 0 };
        try
        {
            var cover = new CoverWindow(new CoverSession(credential, minutes == 0 ? null : TimeSpan.FromMinutes(minutes)),
                (DisplayChoice.SelectedItem as ComboBoxItem)?.Tag as Screen);
            Cover = cover;
            cover.Closed += (_, _) =>
            {
                Cover = null;
                if (_closing) return;
                Show(); Activate();
                Status.Text = cover.Result switch
                {
                    CoverResult.Unlocked => UiText.Get("PINで解除しました。再開時には新しいPINを設定してください。", "Unlocked with your PIN. Set a new PIN before starting again."),
                    CoverResult.TimedOut => UiText.Get("指定した時間になったため解除しました。", "Unlocked because the automatic unlock time was reached."),
                    CoverResult.DisplayChanged => UiText.Get("画面構成が変わったため解除しました。", "Unlocked because the display configuration changed."),
                    CoverResult.TransparencyUnavailable => UiText.Get("この画面環境では半透明表示を利用できません。Linuxでは合成処理が有効なX11セッションを確認してください。", "Translucency is unavailable in this display environment. On Linux, check that your X11 session has compositing enabled."),
                    _ => UiText.Get("カバーを終了しました。", "The cover has closed.")
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
            Status.Text = UiText.Get("カバーを開始できませんでした。設定画面に戻りました。", "Could not start the cover. Returned to setup.");
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
    private readonly Screen? _selectedScreen;
    private Screens? _screens;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _countdown = Ui.Text("", 13, "#B5C6D6");
    internal readonly TextBox Pin = Ui.Secret();
    internal readonly Button Unlock = Ui.Button(UiText.Get("解除", "Unlock"));
    internal readonly TextBlock Feedback = Ui.Text(UiText.Get("開始時に設定したPIN・パスワードを入力してください。", "Enter the PIN or password you set when starting the cover."), 13, "#FFD997");
    internal CoverResult Result => _session.Result;

    public CoverWindow(CoverSession session, Screen? selectedScreen = null)
    {
        _session = session;
        _selectedScreen = selectedScreen;
        AutomationProperties.SetName(Pin, UiText.Get("PIN・パスワード", "PIN or password"));
        Title = UiText.Get("AI Guard Lock — 半透明カバー", "AI Guard Lock — Translucent cover");
        Icon = Ui.Icon();
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false; Topmost = true; ShowInTaskbar = true;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brush.Parse("#700A1422");
        WindowStartupLocation = WindowStartupLocation.Manual;
        var body = new StackPanel { Spacing = 16 };
        body.Children.Add(Ui.Text("AI GUARD LOCK", 12, "#81DBD2"));
        body.Children.Add(Ui.Text(UiText.Get("PINで作業に戻る", "Return to work with your PIN"), 28, "#FFFFFF"));
        body.Children.Add(Ui.Text(UiText.Get("背後の画面はそのままです。本人確認でカバーを解除します。", "Your screen stays visible behind the cover. Enter your PIN to unlock."), 14, "#B5C6D6"));
        body.Children.Add(Pin);
        body.Children.Add(Feedback);
        Unlock.Background = Brush.Parse("#81DBD2"); Unlock.Foreground = Brush.Parse("#10283A");
        Unlock.Click += (_, _) => Submit();
        body.Children.Add(Unlock);
        body.Children.Add(_countdown);
        body.Children.Add(Ui.Text(UiText.Get("OSのロックではありません。", "This is not an OS lock."), 12, "#B5C6D6"));
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
            if (e.Key == Key.Escape) { e.Handled = true; Feedback.Text = UiText.Get("解除するにはPINを入力してください。", "Enter your PIN to unlock."); Pin.Focus(); }
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = _selectedScreen is null
            ? Screens.Primary ?? Screens.ScreenFromWindow(this)
            : Screens.All.FirstOrDefault(candidate => candidate.Equals(_selectedScreen));
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
        Feedback.Text = UiText.Get("PIN・パスワードが違うか、再試行の待機中です。1秒待ってから入力してください。", "Incorrect PIN or password, or retry is paused. Wait one second and try again.");
        Unlock.IsEnabled = _session.CanRetry;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _session.Tick();
        if (Result != CoverResult.Active) { Close(); return; }
        Unlock.IsEnabled = _session.CanRetry;
        var scope = _selectedScreen is null ? UiText.Get("主画面のみ", "Primary screen only") : UiText.Get("選択したモニター", "Selected monitor");
        _countdown.Text = (_session.Remaining is { } remaining
            ? UiText.Get($"自動解除まで {Math.Ceiling(remaining.TotalSeconds):0}秒", $"Automatic unlock in {Math.Ceiling(remaining.TotalSeconds):0}s")
            : UiText.Get("自動解除なし", "No automatic unlock")) + " / " + scope;
    }

    private void OnScreensChanged(object? sender, EventArgs e) => Finish(CoverResult.DisplayChanged);
    internal void Finish(CoverResult result) { _session.Finish(result); Close(); }
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (Result == CoverResult.Active) { e.Cancel = true; Feedback.Text = UiText.Get("解除するにはPINを入力してください。", "Enter your PIN to unlock."); }
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
