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
        Loaded += async (_, _) => await RefreshHelloAsync();
    }

    private async void CheckHello_Click(object sender, RoutedEventArgs e) => await RefreshHelloAsync();

    private async Task RefreshHelloAsync()
    {
        if (_helloBusy || _closing || _overlay is not null) return;
        SetHelloBusy(true);
        _helloAvailable = false;
        HelloStatus.Text = "Windows Helloを確認しています…";
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
            HelloStatus.Text = "Windows Helloの確認に失敗しました。再確認するか、アプリ専用PIN・パスワードを利用してください。";
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
        HelloStatus.Text = "Windowsの本人確認画面で、普段のWindows PINなどを使って認証してください。カバーはまだ開始しません。";
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
                HelloStatus.Text = "Windowsで本人確認できました。Windows Helloで解除する設定を選択しました。";
                StatusText.Text = "確認チェックを入れて「半透明カバーを開始」を押すと使えます。";
            }
            else HelloStatus.Text = WindowsHelloMessages.Probe(result);
        }
        catch (Exception error)
        {
            if (!_closing) HelloStatus.Text = $"Windowsの本人確認を開始できませんでした（エラー 0x{error.HResult:X8}）。カバーは開始していません。";
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
        if (Acknowledge.IsChecked != true)
        {
            StatusText.Text = "注意事項を確認して、チェックを入れてください。";
            return;
        }

        PasswordCredential? credential = null;
        var useHello = HelloMode.IsChecked == true;
        if (useHello && !_helloAvailable)
        {
            StatusText.Text = "Windows Helloの利用可否を再確認してください。";
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
                StatusText.Text = "確認用パスワードが一致していません。";
                ConfirmPassword.Clear();
                ConfirmPassword.Focus();
                return;
            }

            try { credential = new PasswordCredential(password.Characters); }
            catch (ArgumentException)
            {
                StatusText.Text = "数字だけのPINは4〜12桁、パスワードは空白だけを避けて8〜128文字で設定してください。";
                return;
            }
        }

        var minutes = int.Parse((string)((ComboBoxItem)DurationChoice.SelectedItem).Tag, CultureInfo.InvariantCulture);
        try
        {
            _overlay = new OverlayWindow(credential, minutes == 0 ? null : TimeSpan.FromMinutes(minutes), useHello ? _hello : null);
            _overlay.Closed += Overlay_Closed;
            _overlay.Show();
            Hide();
        }
        catch (Exception)
        {
            // Fail open: a partial overlay must never strand the setup window.
            _overlay?.Finish(OverlayResult.Closed);
            _overlay = null;
            credential?.Dispose();
            Show();
            StatusText.Text = "オーバーレイを開始できませんでした。安全のため解除しました。";
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
            OverlayResult.Unlocked => "PIN・パスワードで解除しました。再度開始するには新しく設定してください。",
            OverlayResult.HelloUnlocked => "Windows Helloで本人確認できたため、カバーを解除しました。",
            OverlayResult.TimedOut => "制限時間になったため、自動解除しました。",
            OverlayResult.DisplayChanged => "画面構成が変わったため、安全のため解除しました。",
            _ => "オーバーレイを閉じました（パスワードによる解除ではありません）。"
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
