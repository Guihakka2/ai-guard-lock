using System.Globalization;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using AiGuardLock;
using AiGuardLock.Core;
using Windows.Security.Credentials.UI;

namespace AiGuardLock.Tests;

internal static class Program
{
    private const string TestPassword = "Mvp-Test-482!";
    private static int _passed;
    private static bool _preview;
    private static bool _render;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--hello-status"))
        {
            try
            {
                Console.WriteLine(new WindowsHelloService().CheckAvailabilityAsync().GetAwaiter().GetResult());
                return 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        _preview = args.Contains("--preview");
        _render = args.Contains("--render");
        try { TestCredentials(); }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }

        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("pack://application:,,,/AiGuardLock;component/Theme.xaml") });
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var exitCode = 0;
        app.Startup += async (_, _) =>
        {
            try
            {
                if (_preview) await PreviewWindows();
                else { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja"); await TestWindows(); await TestDisplayScope(); await TestWindowsHello(); await TestEnglish(); }
            }
            catch (Exception e) { Console.Error.WriteLine(e); exitCode = 1; }
            finally
            {
                foreach (Window window in app.Windows.Cast<Window>().ToArray()) { if (window is OverlayWindow cover) cover.Finish(OverlayResult.Closed); else window.Close(); }
                Console.WriteLine($"{_passed} checks passed; exit code {exitCode}.");
                app.Shutdown(exitCode);
            }
        };
        return app.Run();
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {name}");
        Console.WriteLine($"PASS: {name}");
        _passed++;
    }

    private static void TestCredentials()
    {
        using var credential = new PasswordCredential(TestPassword);
        Check(credential.Verify(TestPassword), "correct password");
        Check(!credential.Verify("Mvp-test-482!"), "case-sensitive comparison");
        Check(!credential.Verify("") && !credential.Verify(new string('x', 129)), "reject empty / oversized attempts");
        using var unicode = new PasswordCredential("日本語パスワード🔒");
        Check(unicode.Verify("日本語パスワード🔒") && !unicode.Verify("日本語パスワード🔑"), "Unicode round trip");
        using var spaces = new PasswordCredential(" password ");
        Check(spaces.Verify(" password ") && !spaces.Verify("password"), "no silent password trimming");
        using var pin = new PasswordCredential("0482");
        Check(pin.Verify("0482") && !pin.Verify("482") && !pin.Verify("0483"), "4-digit PIN preserves leading zero and rejects incorrect PIN");
        using var longPin = new PasswordCredential("012345678901");
        Check(longPin.Verify("012345678901"), "12-digit PIN accepted");
        foreach (var invalid in new[] { "", "short12", "        ", new string('x', 129), "123", "1234567890123", "12a4", "１２３４" })
        {
            var rejected = false;
            try { using var unexpected = new PasswordCredential(invalid); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, $"invalid setup length/content ({invalid.Length})");
        }
        using var maximum = new PasswordCredential(new string('z', 128));
        Check(maximum.Verify(new string('z', 128)), "maximum accepted length");
        credential.Dispose();
        credential.Dispose();
        var disposedRejected = false;
        try { credential.Verify(TestPassword); }
        catch (ObjectDisposedException) { disposedRejected = true; }
        Check(disposedRejected, "disposed credential cannot verify");
    }

    private sealed class FakeWindowsHello : IWindowsHelloService
    {
        public UserConsentVerifierAvailability Availability = UserConsentVerifierAvailability.Available;
        public UserConsentVerificationResult Result = UserConsentVerificationResult.Canceled;
        public int Requests;
        public nint Owner;
        public bool ThrowVerification;
        public bool ThrowAvailability;
        public TaskCompletionSource<UserConsentVerificationResult>? Pending;

        public Task<UserConsentVerifierAvailability> CheckAvailabilityAsync() => ThrowAvailability
            ? Task.FromException<UserConsentVerifierAvailability>(new InvalidOperationException("availability failed"))
            : Task.FromResult(Availability);

        public Task<UserConsentVerificationResult> VerifyAsync(nint ownerWindow)
        {
            Requests++;
            Owner = ownerWindow;
            if (ThrowVerification) throw new InvalidOperationException("verification failed");
            return Pending?.Task ?? Task.FromResult(Result);
        }
    }

    private static MainWindow LocalSetup() => new(new FakeWindowsHello { Availability = UserConsentVerifierAvailability.NotConfiguredForUser });

    private static async Task TestWindowsHello()
    {
        var unavailable = LocalSetup();
        unavailable.Show();
        await Task.Delay(50);
        Check(!Control<RadioButton>(unavailable, "HelloMode").IsEnabled && Control<RadioButton>(unavailable, "LocalMode").IsChecked == true,
            "Hello unavailable leaves local PIN mode usable");
        unavailable.Close();

        var probe = new FakeWindowsHello { Availability = UserConsentVerifierAvailability.DeviceNotPresent };
        var probeSetup = new MainWindow(probe);
        probeSetup.Show();
        await Task.Delay(50);
        Render(probeSetup, "setup-hello-probe");
        Check(Control<Button>(probeSetup, "TestHelloButton").IsEnabled && probe.Requests == 0,
            "negative availability still permits explicit probe but never prompts automatically");
        foreach (var result in Enum.GetValues<UserConsentVerificationResult>().Where(r => r != UserConsentVerificationResult.Verified))
        {
            probe.Result = result;
            Click(probeSetup, "TestHelloButton");
            await Task.Delay(20);
            Check(!Control<RadioButton>(probeSetup, "HelloMode").IsEnabled && !Application.Current.Windows.OfType<OverlayWindow>().Any(),
                $"setup probe {result} neither enables Hello nor covers screen");
        }
        probe.ThrowVerification = true;
        Click(probeSetup, "TestHelloButton");
        await Task.Delay(20);
        Check(Control<Button>(probeSetup, "TestHelloButton").IsEnabled && !Control<RadioButton>(probeSetup, "HelloMode").IsEnabled,
            "probe exception leaves setup usable and does not enable Hello");
        probe.ThrowVerification = false;
        probe.Pending = new TaskCompletionSource<UserConsentVerificationResult>();
        var probeBefore = probe.Requests;
        Control<CheckBox>(probeSetup, "Acknowledge").IsChecked = true;
        Click(probeSetup, "TestHelloButton");
        Click(probeSetup, "TestHelloButton");
        Click(probeSetup, "StartButton");
        Check(probe.Requests == probeBefore + 1 && probe.Owner != 0 && !Application.Current.Windows.OfType<OverlayWindow>().Any(),
            "pending setup probe prevents duplicate prompt and cover start");
        probe.Pending.SetResult(UserConsentVerificationResult.Verified);
        await Task.Delay(50);
        Check(Control<RadioButton>(probeSetup, "HelloMode").IsEnabled && Control<RadioButton>(probeSetup, "HelloMode").IsChecked == true,
            "verified probe enables Hello despite negative availability");
        Click(probeSetup, "StartButton");
        Check(CurrentOverlay().IsVisible, "successfully probed Hello can start a cover without local PIN");
        probeSetup.Close();

        probe = new FakeWindowsHello { Availability = UserConsentVerifierAvailability.DeviceNotPresent, Pending = new TaskCompletionSource<UserConsentVerificationResult>() };
        probeSetup = new MainWindow(probe);
        probeSetup.Show();
        await Task.Delay(50);
        Click(probeSetup, "TestHelloButton");
        probeSetup.Close();
        probe.Pending.SetResult(UserConsentVerificationResult.Verified);
        await Task.Delay(50);
        Check(!probeSetup.IsVisible && !Application.Current.Windows.OfType<OverlayWindow>().Any(), "late probe result does not reopen closed setup");

        var hello = new FakeWindowsHello { ThrowAvailability = true };
        var setup = new MainWindow(hello);
        setup.Show();
        await Task.Delay(50);
        Check(Control<Button>(setup, "StartButton").IsEnabled && Control<RadioButton>(setup, "LocalMode").IsChecked == true,
            "availability exception permits local fallback");
        setup.Close();

        hello = new FakeWindowsHello();
        setup = new MainWindow(hello);
        setup.Show();
        await Task.Delay(50);
        Render(setup, "setup-hello");
        Control<CheckBox>(setup, "Acknowledge").IsChecked = true;
        Click(setup, "StartButton");
        var overlay = CurrentOverlay();
        await Task.Delay(50);
        Render(overlay, "overlay-hello");
        Check(overlay.IsVisible && !setup.IsVisible && Control<StackPanel>(overlay, "LocalUnlockPanel").Visibility == Visibility.Collapsed,
            "Hello mode needs no app PIN");
        Check(hello.Requests == 0, "cover never starts biometric prompt automatically");
        foreach (var result in Enum.GetValues<UserConsentVerificationResult>().Where(r => r != UserConsentVerificationResult.Verified))
        {
            hello.Result = result;
            Click(overlay, "HelloUnlockButton");
            await Task.Delay(20);
            Check(overlay.IsVisible && overlay.Result == OverlayResult.Closed && Control<Button>(overlay, "HelloUnlockButton").IsEnabled,
                $"Hello {result} does not unlock and allows retry");
        }
        hello.ThrowVerification = true;
        Click(overlay, "HelloUnlockButton");
        await Task.Delay(20);
        Check(overlay.IsVisible && Control<TextBlock>(overlay, "FeedbackText").Text.Contains("開けません"), "Hello exception does not unlock");
        hello.ThrowVerification = false;
        hello.Pending = new TaskCompletionSource<UserConsentVerificationResult>();
        var before = hello.Requests;
        Click(overlay, "HelloUnlockButton");
        Click(overlay, "HelloUnlockButton");
        Check(hello.Requests == before + 1 && hello.Owner != 0, "Hello requests are single flight and use owner HWND");
        hello.Pending.SetResult(UserConsentVerificationResult.Verified);
        await Task.Delay(50);
        Check(!overlay.IsVisible && setup.IsVisible && overlay.Result == OverlayResult.HelloUnlocked,
            "only verified Hello result unlocks and restores setup");
        setup.Close();

        hello = new FakeWindowsHello { Pending = new TaskCompletionSource<UserConsentVerificationResult>() };
        overlay = new OverlayWindow(null, null, hello);
        overlay.Show();
        Click(overlay, "HelloUnlockButton");
        overlay.Finish(OverlayResult.DisplayChanged);
        hello.Pending.SetResult(UserConsentVerificationResult.Verified);
        await Task.Delay(50);
        Check(overlay.Result == OverlayResult.DisplayChanged && !overlay.IsVisible, "late Hello completion cannot change a finished session");
    }

    private static async Task TestEnglish()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            var setup = LocalSetup();
            setup.Show();
            await Task.Delay(50);
            Check(setup.Title == "AI Guard Lock — Setup", "English setup title");
            Check((string)Control<Button>(setup, "StartButton").Content == "Start screen cover", "English start button");
            Check((string)((ComboBoxItem)Control<ComboBox>(setup, "DisplayChoice").Items[1]).Content == "All monitors", "English display scope option");
            Check(AutomationProperties.GetName(Control<PasswordBox>(setup, "NewPassword")) == "PIN or password", "English accessible PIN name");
            Render(setup, "setup-en");
            Click(setup, "StartButton");
            Check(Control<TextBlock>(setup, "StatusText").Text == "Read the notices and check the acknowledgment box.", "English acknowledgment validation");
            Control<CheckBox>(setup, "Acknowledge").IsChecked = true;
            FillSetup(setup);
            Click(setup, "StartButton");
            var cover = CurrentOverlay();
            await Task.Delay(50);
            Check(cover.Title == "AI Guard Lock — Screen cover", "English cover title");
            Check(Control<TextBlock>(cover, "CountdownText").Text == "No automatic release  /  Primary screen only", "English countdown");
            Render(cover, "overlay-en");
            Control<PasswordBox>(cover, "UnlockPassword").Password = "wrong";
            Click(cover, "UnlockButton");
            Check(Control<TextBlock>(cover, "FeedbackText").Text == "Incorrect PIN or password. Try again in one second.", "English retry feedback");
            cover.Finish(OverlayResult.DisplayChanged);
            Check(Control<TextBlock>(setup, "StatusText").Text == "The display configuration changed. The cover was released for safety.", "English recovery status");
            setup.Close();
            var hello = new FakeWindowsHello();
            setup = new MainWindow(hello);
            setup.Show();
            await Task.Delay(50);
            Render(setup, "setup-hello-en");
            foreach (var result in Enum.GetValues<UserConsentVerifierAvailability>())
                Check(!WindowsHelloMessages.Availability(result).Any(c => c >= '\u3000' && c <= '\u9fff'), $"English Hello availability {result}");
            foreach (var result in Enum.GetValues<UserConsentVerificationResult>())
            {
                hello.Result = result;
                Click(setup, "TestHelloButton");
                await Task.Delay(20);
                Check(!Control<TextBlock>(setup, "HelloStatus").Text.Any(c => c >= '\u3000' && c <= '\u9fff'), $"English Hello probe {result}");
            }
            Control<CheckBox>(setup, "Acknowledge").IsChecked = true;
            Click(setup, "StartButton");
            cover = CurrentOverlay();
            await Task.Delay(50);
            Render(cover, "overlay-hello-en");
            foreach (var result in Enum.GetValues<UserConsentVerificationResult>().Where(r => r != UserConsentVerificationResult.Verified))
            {
                hello.Result = result;
                Click(cover, "HelloUnlockButton");
                await Task.Delay(20);
                Check(cover.IsVisible && !Control<TextBlock>(cover, "FeedbackText").Text.Any(c => c >= '\u3000' && c <= '\u9fff'), $"English Hello rejection {result}");
            }
            hello.Result = UserConsentVerificationResult.Verified;
            Click(cover, "HelloUnlockButton");
            await Task.Delay(20);
            Check(cover.Result == OverlayResult.HelloUnlocked, "English verified Hello unlock");
            setup.Close();
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static T Control<T>(Window window, string name) where T : FrameworkElement => (T)window.FindName(name);

    private static async Task TestDisplayScope()
    {
        var native = DisplayMonitors.GetAll();
        var selectionSetup = LocalSetup();
        Check(Control<ComboBox>(selectionSetup, "DisplayChoice").Items.Count == native.Count + 2,
            "setup lists each connected monitor alongside primary and all");
        selectionSetup.Close();
        Check(native.Count >= 1 && native.Count(m => m.IsPrimary) == 1, "native monitor enumeration includes primary");
        var primary = native.Single(m => m.IsPrimary);
        MonitorBounds[] simulated = [primary, new(-800, -200, 0, 400, false)];
        using var selectedCredential = new PasswordCredential(TestPassword);
        var selectedCover = new OverlayWindow(selectedCredential, null, null,
            getMonitors: () => simulated, selectedMonitor: simulated[1]);
        selectedCover.ShowCover();
        await Task.Delay(80);
        GetWindowRect(new System.Windows.Interop.WindowInteropHelper(selectedCover).Handle, out var selectedRect);
        Check(selectedRect.Left == -800 && selectedRect.Top == -200 && selectedRect.Right == 0 && selectedRect.Bottom == 400,
            "selected monitor places authentication cover at full negative native bounds");
        Check(!Application.Current.Windows.OfType<SecondaryCoverWindow>().Any(), "selected monitor leaves other screens uncovered");
        Control<PasswordBox>(selectedCover, "UnlockPassword").Password = TestPassword;
        Click(selectedCover, "UnlockButton");
        Check(!selectedCover.IsVisible && selectedCover.Result == OverlayResult.Unlocked, "selected monitor unlocks with PIN");
        using var missingCredential = new PasswordCredential(TestPassword);
        var missingCover = new OverlayWindow(missingCredential, null, null,
            getMonitors: () => new[] { primary }, selectedMonitor: simulated[1]);
        var missingRejected = false;
        try { missingCover.ShowCover(); }
        catch (InvalidOperationException) { missingRejected = true; }
        Check(missingRejected && !missingCover.IsVisible, "disconnected selection never covers another monitor");
        using var credential = new PasswordCredential(TestPassword);
        var cover = new OverlayWindow(credential, null, null, true, () => simulated);
        cover.ShowCover();
        await Task.Delay(80);
        var secondary = Application.Current.Windows.OfType<SecondaryCoverWindow>().Single();
        Check(secondary.IsVisible && secondary.Topmost && secondary.AllowsTransparency && secondary.Owner == cover,
            "all monitors creates owned translucent secondary cover");
        var nativeRect = new TestRect();
        GetWindowRect(new System.Windows.Interop.WindowInteropHelper(secondary).Handle, out nativeRect);
        Check(nativeRect.Left == -800 && nativeRect.Top == -200 && nativeRect.Right == 0 && nativeRect.Bottom == 400,
            "secondary uses full native bounds including negative coordinates");
        secondary.Close();
        Check(secondary.IsVisible, "secondary close cannot bypass authentication");
        Control<PasswordBox>(cover, "UnlockPassword").Password = "wrong";
        Click(cover, "UnlockButton");
        Check(secondary.IsVisible && cover.IsVisible, "wrong password keeps every monitor covered");
        await Task.Delay(1200);
        Control<PasswordBox>(cover, "UnlockPassword").Password = TestPassword;
        Click(cover, "UnlockButton");
        Check(!cover.IsVisible && !secondary.IsVisible && cover.Result == OverlayResult.Unlocked,
            "correct PIN releases all monitors");

        foreach (var result in new[] { OverlayResult.DisplayChanged, OverlayResult.Closed })
        {
            using var nextCredential = new PasswordCredential(TestPassword);
            cover = new OverlayWindow(nextCredential, null, null, true, () => simulated);
            cover.ShowCover();
            secondary = Application.Current.Windows.OfType<SecondaryCoverWindow>().Single();
            cover.Finish(result);
            Check(!secondary.IsVisible && !cover.IsVisible, $"{result} releases all monitors");
        }
        var hello = new FakeWindowsHello { Result = UserConsentVerificationResult.Verified };
        cover = new OverlayWindow(null, null, hello, true, () => simulated);
        cover.ShowCover();
        secondary = Application.Current.Windows.OfType<SecondaryCoverWindow>().Single();
        Click(cover, "HelloUnlockButton");
        await Task.Delay(30);
        Check(!secondary.IsVisible && cover.Result == OverlayResult.HelloUnlocked, "Hello success releases all monitors");

        using var timedCredential = new PasswordCredential(TestPassword);
        cover = new OverlayWindow(timedCredential, TimeSpan.FromMilliseconds(150), null, true, () => simulated);
        cover.ShowCover();
        secondary = Application.Current.Windows.OfType<SecondaryCoverWindow>().Single();
        await Task.Delay(600);
        Check(!secondary.IsVisible && cover.Result == OverlayResult.TimedOut, "timeout releases all monitors");

        using var singleCredential = new PasswordCredential(TestPassword);
        cover = new OverlayWindow(singleCredential, null, null, true, () => new[] { primary });
        cover.ShowCover();
        Check(cover.IsVisible && !Application.Current.Windows.OfType<SecondaryCoverWindow>().Any(), "all monitors works with one monitor");
        cover.Finish(OverlayResult.Closed);

        using var failedCredential = new PasswordCredential(TestPassword);
        cover = new OverlayWindow(failedCredential, null, null, true,
            () => new[] { primary, simulated[1], new MonitorBounds(10, 10, 10, 20, false) });
        var failed = false;
        try { cover.ShowCover(); }
        catch (ArgumentException) { failed = true; }
        Check(failed && !cover.IsVisible && !Application.Current.Windows.OfType<SecondaryCoverWindow>().Any(),
            "partial startup failure releases primary and all secondary covers");

        var setup = LocalSetup();
        setup.Show();
        await Task.Delay(50);
        Control<ComboBox>(setup, "DisplayChoice").SelectedIndex = 1;
        Control<CheckBox>(setup, "Acknowledge").IsChecked = true;
        FillSetup(setup);
        Click(setup, "StartButton");
        await Task.Delay(80);
        cover = CurrentOverlay();
        Check(Application.Current.Windows.OfType<SecondaryCoverWindow>().Count() == native.Count - 1,
            "setup all-monitor selection covers every connected monitor");
        Check(Control<TextBlock>(cover, "CountdownText").Text.Contains("すべてのモニター"), "Japanese all-monitor countdown");
        cover.Finish(OverlayResult.DisplayChanged);
        Check(setup.IsVisible && Control<ComboBox>(setup, "DisplayChoice").SelectedIndex == 1, "selection retained after release");
        FillSetup(setup);
        Click(setup, "StartButton");
        setup.Close();
        Check(!Application.Current.Windows.OfType<SecondaryCoverWindow>().Any(), "closing setup releases all secondary covers");
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TestRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out TestRect rect);
    private static void Click(Window window, string name) => Control<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static OverlayWindow CurrentOverlay() => Application.Current.Windows.OfType<OverlayWindow>().Single();
    private static void FillSetup(MainWindow setup)
    {
        Control<PasswordBox>(setup, "NewPassword").Password = TestPassword;
        Control<PasswordBox>(setup, "ConfirmPassword").Password = TestPassword;
    }

    private static void Render(Window window, string name)
    {
        if (!_render) return;
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),
            (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(".artifacts/render");
        using var stream = File.Create(Path.Combine(".artifacts/render", name + ".png"));
        encoder.Save(stream);
    }

    private static async Task PreviewWindows()
    {
        // Visual review only; do not mix user interaction with automated assertions.
        var setup = LocalSetup();
        setup.Show();
        Console.WriteLine("PREVIEW: setup (30 seconds)");
        await Task.Delay(30000);
        setup.Close();
        using var credential = new PasswordCredential(TestPassword);
        var overlay = new OverlayWindow(credential, TimeSpan.FromSeconds(45));
        overlay.Show();
        Console.WriteLine("PREVIEW: overlay (45 seconds)");
        await Task.Delay(46000);
        overlay.Close();
    }

    private static async Task TestWindows()
    {
        var setup = LocalSetup();
        Application.Current.MainWindow = setup;
        setup.Show();
        await Task.Delay(100);
        var displayChoice = setup.FindName("DisplayChoice") as ComboBox;
        Check(displayChoice is not null && displayChoice.Items.Count == DisplayMonitors.GetAll().Count + 2 && displayChoice.SelectedIndex == 0,
            "setup offers primary or all monitors and defaults to primary");
        Render(setup, "setup");
        FillSetup(setup);
        Click(setup, "StartButton");
        Check(!Application.Current.Windows.OfType<OverlayWindow>().Any(), "acknowledgment required");
        Control<CheckBox>(setup, "Acknowledge").IsChecked = true;
        Control<PasswordBox>(setup, "ConfirmPassword").Password = "Different-123";
        Click(setup, "StartButton");
        Check(!Application.Current.Windows.OfType<OverlayWindow>().Any(), "mismatched setup cannot start");
        FillSetup(setup);
        Click(setup, "StartButton");
        await Task.Delay(100);
        var overlay = CurrentOverlay();
        Render(overlay, "overlay");
        Check(Control<TextBlock>(overlay, "CountdownText").Text.Contains("自動解除なし"), "default cover has no automatic unlock");
        Check(overlay.IsVisible && !setup.IsVisible && overlay.Topmost && overlay.AllowsTransparency,
            "transparent topmost overlay opens; setup hides");
        Check(overlay.Left == 0 && overlay.Top == 0 && overlay.Width == SystemParameters.PrimaryScreenWidth
            && overlay.Height == SystemParameters.PrimaryScreenHeight, "primary screen dimensions");
        Check(Control<PasswordBox>(setup, "NewPassword").SecurePassword.Length == 0
            && Control<PasswordBox>(setup, "ConfirmPassword").SecurePassword.Length == 0, "setup fields cleared");
        Control<PasswordBox>(overlay, "UnlockPassword").Password = "Wrong-Test-123";
        Click(overlay, "UnlockButton");
        Check(overlay.IsVisible && !Control<Button>(overlay, "UnlockButton").IsEnabled, "wrong password stays covered with retry delay");
        Check(Control<PasswordBox>(overlay, "UnlockPassword").SecurePassword.Length == 0, "failed attempt cleared");
        // Deliberately raise the event despite disabled UI, to test handler-level throttling.
        Control<PasswordBox>(overlay, "UnlockPassword").Password = TestPassword;
        Click(overlay, "UnlockButton");
        Check(overlay.IsVisible, "rapid retries cannot bypass cooldown");
        await Task.Delay(1300);
        Check(Control<Button>(overlay, "UnlockButton").IsEnabled, "retry recovers without blocking dispatcher");
        Click(overlay, "UnlockButton");
        Check(overlay.Result == OverlayResult.Unlocked && !overlay.IsVisible && setup.IsVisible,
            "correct password closes overlay and restores setup");

        FillSetup(setup);
        Click(setup, "StartButton");
        overlay = CurrentOverlay();
        overlay.Close();
        Check(overlay.IsVisible && !setup.IsVisible, "window close cannot bypass password");
        overlay.Finish(OverlayResult.DisplayChanged);
        Check(setup.IsVisible, "display change restores setup");

        FillSetup(setup);
        Click(setup, "StartButton");
        overlay = CurrentOverlay();
        setup.Close();
        Check(!overlay.IsVisible, "closing setup also closes its overlay");

        using var timeoutCredential = new PasswordCredential(TestPassword);
        overlay = new OverlayWindow(timeoutCredential, TimeSpan.FromMilliseconds(300));
        overlay.Show();
        // Incorrect passwords must not reset the auto-release timer.
        Control<PasswordBox>(overlay, "UnlockPassword").Password = "Wrong-Test-123";
        Click(overlay, "UnlockButton");
        await Task.Delay(800);
        Check(overlay.Result == OverlayResult.TimedOut && !overlay.IsVisible, "auto-release survives a failed attempt");

        using var escCredential = new PasswordCredential(TestPassword);
        overlay = new OverlayWindow(escCredential, TimeSpan.FromSeconds(10));
        overlay.Show();
        overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(overlay), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check(overlay.IsVisible, "Escape cannot bypass password");
        overlay.Finish(OverlayResult.Closed);

        using var displayCredential = new PasswordCredential(TestPassword);
        overlay = new OverlayWindow(displayCredential, TimeSpan.FromSeconds(10));
        overlay.Show();
        overlay.Finish(OverlayResult.DisplayChanged);
        Check(!overlay.IsVisible && overlay.Result == OverlayResult.DisplayChanged, "display-change recovery handler");


    }
}
