using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AiGuardLock.Core;
using Microsoft.Win32;
using System.Windows.Interop;
using Windows.Security.Credentials.UI;

namespace AiGuardLock;

public enum OverlayResult { Closed, Unlocked, TimedOut, EmergencyExit, DisplayChanged, HelloUnlocked }

public partial class OverlayWindow : Window
{
    private readonly PasswordCredential? _credential;
    private readonly IWindowsHelloService? _hello;
    private bool _verificationInProgress;
    private readonly TimeSpan? _duration;
    private readonly Stopwatch _elapsed = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TimeSpan _retryAfter;
    private bool _closed;
    private bool _allowClose;
    public OverlayResult Result { get; private set; } = OverlayResult.Closed;

    public OverlayWindow(PasswordCredential credential, TimeSpan? duration) : this(credential, duration, null) { }

    internal OverlayWindow(PasswordCredential? credential, TimeSpan? duration, IWindowsHelloService? hello)
    {
        if (credential is null && hello is null) throw new ArgumentException("解除方法が必要です。");
        if (duration is { } limit && (limit <= TimeSpan.Zero || limit > TimeSpan.FromHours(1)))
            throw new ArgumentOutOfRangeException(nameof(duration));
        _credential = credential;
        _hello = hello;
        _duration = duration;
        InitializeComponent();
        LocalUnlockPanel.Visibility = credential is null ? Visibility.Collapsed : Visibility.Visible;
        HelloUnlockPanel.Visibility = hello is null ? Visibility.Collapsed : Visibility.Visible;
        if (credential is null)
        {
            UnlockInstructions.Text = "背後の画面はそのままです。Windows Helloの本人確認で、このカバーだけを解除します。";
            FeedbackText.Text = "「Windows Helloで解除」を押して本人確認してください。";
            HelloUnlockButton.IsDefault = true;
            UnlockButton.IsDefault = false;
        }
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Loaded += OnLoaded;
        _timer.Tick += OnTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _elapsed.Start();
        UpdateCountdown();
        _timer.Start();
        FocusUnlockControl();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_closed) return;
        if (_duration is { } duration && _elapsed.Elapsed >= duration)
        {
            Finish(OverlayResult.TimedOut);
            return;
        }
        UnlockButton.IsEnabled = _elapsed.Elapsed >= _retryAfter;
        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (_duration is null)
        {
            CountdownText.Text = "自動解除なし  /  主画面のみ";
            return;
        }
        var seconds = Math.Max(0, (int)Math.Ceiling((_duration.Value - _elapsed.Elapsed).TotalSeconds));
        CountdownText.Text = $"自動解除まで {seconds / 60:00}:{seconds % 60:00}  /  プライマリ画面のみ";
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _credential is null || _elapsed.Elapsed < _retryAfter) return;
        if (_duration is { } duration && _elapsed.Elapsed >= duration) { Finish(OverlayResult.TimedOut); return; }
        using var secure = UnlockPassword.SecurePassword;
        using var password = new PasswordBuffer(secure);
        var valid = _credential.Verify(password.Characters);
        UnlockPassword.Clear();
        if (valid) { Finish(OverlayResult.Unlocked); return; }
        FeedbackText.Text = "PIN・パスワードが違います。1秒後に再試行できます。";
        _retryAfter = _elapsed.Elapsed + TimeSpan.FromSeconds(1);
        UnlockButton.IsEnabled = false;
        UnlockPassword.Focus();
    }

    private async void HelloUnlock_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _hello is null || _verificationInProgress) return;
        _verificationInProgress = true;
        HelloUnlockButton.IsEnabled = false;
        FeedbackText.Text = "Windowsの本人確認ダイアログで操作してください。";
        try
        {
            var result = await _hello.VerifyAsync(new WindowInteropHelper(this).Handle);
            if (_closed) return;
            if (result == UserConsentVerificationResult.Verified) Finish(OverlayResult.HelloUnlocked);
            else FeedbackText.Text = WindowsHelloMessages.Verification(result);
        }
        catch (Exception)
        {
            if (!_closed) FeedbackText.Text = "Windows Helloを開けませんでした。カバーは解除していません。再試行してください。";
        }
        finally
        {
            _verificationInProgress = false;
            if (!_closed)
            {
                HelloUnlockButton.IsEnabled = true;
                FocusUnlockControl();
            }
        }
    }

    private void FocusUnlockControl()
    {
        if (_credential is null) HelloUnlockButton.Focus();
        else UnlockPassword.Focus();
    }

    internal void Finish(OverlayResult result)
    {
        if (_closed) return;
        Result = result;
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            FeedbackText.Text = "解除するには本人確認してください。";
            FocusUnlockControl();
        }
        base.OnClosing(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => Finish(OverlayResult.DisplayChanged)));
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _elapsed.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        UnlockPassword.Clear();
        _credential?.Dispose();
        base.OnClosed(e);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        FeedbackText.Text = "解除するには本人確認してください。";
        FocusUnlockControl();
    }
}
