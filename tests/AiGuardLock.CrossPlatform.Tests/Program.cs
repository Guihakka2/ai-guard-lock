using AiGuardLock.Core;
using AiGuardLock.CrossPlatform;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.Media.Imaging;

internal static class Program
{
    private static int _passed;
    private sealed class Clock : TimeProvider
    {
        public long Ticks;
        public override long GetTimestamp() => Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration) => Ticks += duration.Ticks;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--native-smoke")) return NativeSmoke();
            var time = new Clock();
            using var credential = new PasswordCredential("0482");
            using var session = new CoverSession(credential, null, time);
            Check(session.Result == CoverResult.Active && session.Remaining is null, "no timeout by default");
            Check(!session.TryUnlock("0483"), "wrong PIN rejected");
            Check(!session.TryUnlock("0482"), "correct PIN cannot bypass retry interval");
            time.Advance(TimeSpan.FromSeconds(1));
            Check(session.TryUnlock("0482") && session.Result == CoverResult.Unlocked, "PIN unlock after interval");
            session.Finish(CoverResult.DisplayChanged);
            Check(session.Result == CoverResult.Unlocked && !session.TryUnlock("0482"), "finished session cannot change result or reverify");
            var disposed = false;
            try { credential.Verify("0482"); } catch (ObjectDisposedException) { disposed = true; }
            Check(disposed, "credential cleared on finish");
            using var expiring = new CoverSession(new PasswordCredential("test-code"), TimeSpan.FromSeconds(2), time);
            expiring.TryUnlock("wrong-code");
            time.Advance(TimeSpan.FromSeconds(3));
            Check(!expiring.TryUnlock("test-code") && expiring.Result == CoverResult.TimedOut, "timeout cannot be extended or bypassed by PIN");
            using var indefinite = new CoverSession(new PasswordCredential("1234"), null, time);
            time.Advance(TimeSpan.FromDays(2));
            indefinite.Tick();
            Check(indefinite.Result == CoverResult.Active, "indefinite cover survives elapsed time");
            indefinite.Dispose();
            Check(indefinite.Result == CoverResult.Closed, "disposal closes session");
            foreach (var duration in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.FromHours(2) })
            {
                using var invalidCredential = new PasswordCredential("1234");
                var rejected = false;
                try { using var invalid = new CoverSession(invalidCredential, duration); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "invalid timeout rejected: " + duration);
            }

            AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            var setup = new SetupWindow();
            setup.Show();
            Check(setup.Icon is not null && setup.Cover is null, "setup icon loads and no cover starts automatically");
            setup.Pin.Text = "0482"; setup.Confirmation.Text = "0482";
            setup.StartCover();
            Check(setup.Cover is null, "acknowledgment required");
            setup.Acknowledge.IsChecked = true;
            setup.Confirmation.Text = "0483";
            setup.StartCover();
            Check(setup.Cover is null && setup.Confirmation.Text == string.Empty, "mismatched PIN rejected and cleared");
            setup.Close();
            Check(setup.Pin.Text == string.Empty, "setup clears PIN on close");
            using var viewSession = new CoverSession(new PasswordCredential("0482"), null, time);
            var view = new CoverWindow(viewSession);
            Check(view.WindowDecorations == Avalonia.Controls.WindowDecorations.None, "cover has no operating-system window decorations");
            view.Close();
            Check(view.Result == CoverResult.Active, "ordinary close request cannot finish cover");
            view.Pin.Text = "9999";
            view.Submit();
            Check(view.Result == CoverResult.Active && view.Pin.Text == string.Empty, "view clears incorrect PIN without unlocking");
            time.Advance(TimeSpan.FromSeconds(1));
            view.Pin.Text = "0482";
            view.Submit();
            Check(view.Result == CoverResult.Unlocked, "view unlocks only with valid PIN");
            Console.WriteLine($"{_passed} checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void Pump(int milliseconds)
    {
        using var cancel = new CancellationTokenSource(milliseconds);
        Dispatcher.UIThread.MainLoop(cancel.Token);
    }

    private static int NativeSmoke()
    {
        AppBuilder.Configure<App>().UsePlatformDetect().SetupWithoutStarting();
        var setup = new SetupWindow();
        try
        {
            setup.Show(); Pump(300);
            Directory.CreateDirectory(".artifacts/render");
            using (var image = new RenderTargetBitmap(new PixelSize((int)setup.ClientSize.Width, (int)setup.ClientSize.Height)))
            {
                image.Render(setup); image.Save(".artifacts/render/cross-setup.png", new PngBitmapEncoderOptions());
            }
            setup.Pin.Text = "0482"; setup.Confirmation.Text = "0482"; setup.Acknowledge.IsChecked = true;
            setup.StartCover(); Pump(400);
            var cover = setup.Cover ?? throw new InvalidOperationException("Native transparency was not accepted: " + setup.Status.Text);
            Check(cover.IsVisible && !setup.IsVisible && cover.Topmost, "native transparent cover opens and hides setup");
            using (var image = new RenderTargetBitmap(new PixelSize((int)cover.ClientSize.Width, (int)cover.ClientSize.Height)))
            {
                image.Render(cover); image.Save(".artifacts/render/cross-cover.png", new PngBitmapEncoderOptions());
            }
            cover.Close();
            Check(cover.IsVisible, "native close request is refused");
            cover.Pin.Text = "9999"; cover.Submit();
            Check(cover.IsVisible, "native wrong PIN stays covered");
            Pump(1200);
            cover.Pin.Text = "0482"; cover.Submit(); Pump(100);
            Check(!cover.IsVisible && setup.IsVisible, "native correct PIN restores setup");
            Console.WriteLine($"{_passed} native checks passed.");
            return 0;
        }
        finally { setup.Close(); }
    }
}
