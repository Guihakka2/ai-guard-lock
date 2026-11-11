using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using AiGuardLock.Core;
using Windows.Security.Credentials.UI;

namespace AiGuardLock;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private bool _closing;
    private readonly IWindowsHelloService _hello;
    private bool _helloAvailable;
    private bool _helloBusy;

    public MainWindow() : this(new WindowsHelloService()) { }

    internal MainWindow(IWindowsHelloService hello)
    {
        _hello = hello;
        InitializeComponent();
        RefreshDisplays();
        DisplayChoice.DropDownOpened += (_, _) => RefreshDisplays();
        Loaded += async (_, _) => await RefreshHelloAsync();
    }

    private void RefreshDisplays()
    {
        var selected = (DisplayChoice.SelectedItem as ComboBoxItem)?.Tag;
        var index = DisplayChoice.SelectedIndex;
        try
        {
            var monitors = DisplayMonitors.GetAll();
            while (DisplayChoice.Items.Count > 2) DisplayChoice.Items.RemoveAt(2);
            for (var i = 0; i < monitors.Count; i++)
            {
                var monitor = monitors[i];
                var label = UiText.Get($"モニター {i + 1}", $"Monitor {i + 1}");
                if (monitor.IsPrimary) label += UiText.Get("（主画面）", " (primary)");
                var item = new ComboBoxItem
                {
                    Content = $"{label} — {monitor.Right - monitor.Left} × {monitor.Bottom - monitor.Top} ({monitor.Left}, {monitor.Top})",
                    Tag = monitor
                };
                DisplayChoice.Items.Add(item);
                if (selected is MonitorBounds previous && previous == monitor) DisplayChoice.SelectedItem = item;
            }
            if (selected is not MonitorBounds) DisplayChoice.SelectedIndex = index;
            // A disconnected selection must be chosen again, never silently replaced.
        }
        catch (Exception)
        {
            StatusText.Text = UiText.Get("画面一覧を取得できません。もう一度開いてください。", "Could not list monitors. Open the list to retry.");
        }
    }

    private async void CheckHello_Click(object sender, RoutedEventArgs e) => await RefreshHelloAsync();

    private async Task RefreshHelloAsync()
    {
        if (_helloBusy || _closing || _overlay is not null) return;
        SetHelloBusy(true);
        _helloAvailable = false;
        HelloStatus.Text = UiText.Get("Windows Helloを確認しています…", "Checking Windows Hello…");
        try
        {
            var availability = await _hello.CheckAvailabilityAsync();
            if (_closing) return;
            _helloAvailable = availability == UserConsentVerifierAvailability.Available;
            HelloStatus.Text = WindowsHelloMessages.Availability(availability);
        }
        catch (Exception)
        {
            if (_closing) return;
            HelloStatus.Text = UiText.Get("Windows Helloの確認に失敗しました。再確認するか、アプリ専用PIN・パスワードを利用してください。", "Could not check Windows Hello. Check again or use an app-only PIN or password.");
        }
        finally
        {
            if (!_closing)
            {
                HelloMode.IsEnabled = _helloAvailable;
                if (!_helloAvailable) LocalMode.IsChecked = true;
                SetHelloBusy(false);
            }
        }
    }

    private async void TestHello_Click(object sender, RoutedEventArgs e)
    {
        if (_helloBusy || _closing || _overlay is not null) return;
        SetHelloBusy(true);
        HelloStatus.Text = UiText.Get("Windowsの本人確認画面で、普段のWindows PINなどを使って認証してください。カバーはまだ開始しません。", "Use your usual Windows PIN or another method in the Windows verification dialog. The cover has not started.");
        try
        {
            // The availability result is advisory for this explicit, user-initiated probe.
            // Only a genuine Verified result enables an otherwise unavailable Hello mode.
            var result = await _hello.VerifyAsync(new WindowInteropHelper(this).Handle);
            if (_closing) return;
            if (result == UserConsentVerificationResult.Verified)
            {
                _helloAvailable = true;
                HelloMode.IsEnabled = true;
                HelloMode.IsChecked = true;
                HelloStatus.Text = UiText.Get("Windowsで本人確認できました。Windows Helloで解除する設定を選択しました。", "Windows verified your identity. Windows Hello is now selected for unlocking.");
                StatusText.Text = UiText.Get("確認チェックを入れて「半透明カバーを開始」を押すと使えます。", "Check the acknowledgment box and select Start screen cover.");
            }
            else HelloStatus.Text = WindowsHelloMessages.Probe(result);
        }
        catch (Exception error)
        {
            if (!_closing) HelloStatus.Text = UiText.Get($"Windowsの本人確認を開始できませんでした（エラー 0x{error.HResult:X8}）。カバーは開始していません。", $"Could not start Windows verification (error 0x{error.HResult:X8}). The cover has not started.");
        }
        finally
        {
            if (!_closing) SetHelloBusy(false);
        }
    }

    private void SetHelloBusy(bool busy)
    {
        _helloBusy = busy;
        CheckHelloButton.IsEnabled = !busy;
        TestHelloButton.IsEnabled = !busy;
        StartButton.IsEnabled = !busy;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay is not null || _helloBusy) return;
        if (DisplayChoice.SelectedIndex < 0)
        {
            StatusText.Text = UiText.Get("カバーする画面を選択してください。", "Select a monitor to cover.");
            return;
        }
        if (Acknowledge.IsChecked != true)
        {
            StatusText.Text = UiText.Get("注意事項を確認して、チェックを入れてください。", "Read the notices and check the acknowledgment box.");
            return;
        }

        PasswordCredential? credential = null;
        var useHello = HelloMode.IsChecked == true;
        if (useHello && !_helloAvailable)
        {
            StatusText.Text = UiText.Get("Windows Helloの利用可否を再確認してください。", "Check Windows Hello availability again.");
            return;
        }
        if (!useHello)
        {
            using var securePassword = NewPassword.SecurePassword;
            using var secureConfirmation = ConfirmPassword.SecurePassword;
            using var password = new PasswordBuffer(securePassword);
            using var confirmation = new PasswordBuffer(secureConfirmation);
            if (!password.Characters.SequenceEqual(confirmation.Characters))
            {
                StatusText.Text = UiText.Get("確認用パスワードが一致していません。", "The confirmation password does not match.");
                ConfirmPassword.Clear();
                ConfirmPassword.Focus();
                return;
            }

            try { credential = new PasswordCredential(password.Characters); }
            catch (ArgumentException)
            {
                StatusText.Text = UiText.Get("数字だけのPINは4〜12桁、パスワードは空白だけを避けて8〜128文字で設定してください。", "Use a 4–12 digit PIN or an 8–128 character password that is not all whitespace.");
                return;
            }
        }

        var minutes = int.Parse((string)((ComboBoxItem)DurationChoice.SelectedItem).Tag, CultureInfo.InvariantCulture);
        try
        {
            _overlay = new OverlayWindow(credential, minutes == 0 ? null : TimeSpan.FromMinutes(minutes), useHello ? _hello : null,
                DisplayChoice.SelectedIndex == 1,
                selectedMonitor: (DisplayChoice.SelectedItem as ComboBoxItem)?.Tag as MonitorBounds?);
            _overlay.Closed += Overlay_Closed;
            _overlay.ShowCover();
            Hide();
        }
        catch (Exception)
        {
            // Fail open: a partial overlay must never strand the setup window.
            _overlay?.Finish(OverlayResult.Closed);
            _overlay = null;
            credential?.Dispose();
            Show();
            StatusText.Text = UiText.Get("オーバーレイを開始できませんでした。安全のため解除しました。", "Could not start the cover. It was released for safety.");
        }
        finally
        {
            NewPassword.Clear();
            ConfirmPassword.Clear();
        }
    }

    private void Overlay_Closed(object? sender, EventArgs e)
    {
        var result = ((OverlayWindow)sender!).Result;
        _overlay = null;
        if (_closing) return;
        if (result == OverlayResult.EmergencyExit)
        {
            Close();
            return;
        }
        Show();
        Activate();
        StatusText.Text = result switch
        {
            OverlayResult.Unlocked => UiText.Get("PIN・パスワードで解除しました。再度開始するには新しく設定してください。", "Unlocked with your PIN or password. Set a new one to start again."),
            OverlayResult.HelloUnlocked => UiText.Get("Windows Helloで本人確認できたため、カバーを解除しました。", "Windows Hello verified your identity and released the cover."),
            OverlayResult.TimedOut => UiText.Get("制限時間になったため、自動解除しました。", "The time limit was reached. The cover was released automatically."),
            OverlayResult.DisplayChanged => UiText.Get("画面構成が変わったため、安全のため解除しました。", "The display configuration changed. The cover was released for safety."),
            _ => UiText.Get("オーバーレイを閉じました（パスワードによる解除ではありません）。", "The cover was closed without password verification.")
        };
        if (HelloMode.IsChecked == true) StartButton.Focus();
        else NewPassword.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _closing = true;
        _overlay?.Finish(OverlayResult.Closed);
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }
}
