using AiGuardLock.Core;

namespace AiGuardLock.CrossPlatform;

internal enum CoverResult { Active, Unlocked, TimedOut, DisplayChanged, Closed, TransparencyUnavailable }

internal sealed class CoverSession : IDisposable
{
    private readonly PasswordCredential _credential;
    private readonly TimeProvider _time;
    private readonly long _started;
    private readonly TimeSpan? _duration;
    private TimeSpan _retryAt;
    public CoverResult Result { get; private set; } = CoverResult.Active;
    public TimeSpan Elapsed => _time.GetElapsedTime(_started);
    public bool CanRetry => Result == CoverResult.Active && Elapsed >= _retryAt;
    public TimeSpan? Remaining => _duration is { } duration ? TimeSpan.FromSeconds(Math.Max(0, (duration - Elapsed).TotalSeconds)) : null;

    public CoverSession(PasswordCredential credential, TimeSpan? duration, TimeProvider? time = null)
    {
        if (duration is { } limit && (limit <= TimeSpan.Zero || limit > TimeSpan.FromHours(1)))
            throw new ArgumentOutOfRangeException(nameof(duration));
        _credential = credential;
        _duration = duration;
        _time = time ?? TimeProvider.System;
        _started = _time.GetTimestamp();
    }

    public void Tick()
    {
        if (Result == CoverResult.Active && _duration is { } duration && Elapsed >= duration)
            Finish(CoverResult.TimedOut);
    }

    public bool TryUnlock(ReadOnlySpan<char> value)
    {
        Tick();
        if (!CanRetry) return false;
        if (_credential.Verify(value)) { Finish(CoverResult.Unlocked); return true; }
        _retryAt = Elapsed + TimeSpan.FromSeconds(1);
        return false;
    }

    public void Finish(CoverResult result)
    {
        if (result == CoverResult.Active) throw new ArgumentException("A completed result is required.", nameof(result));
        if (Result != CoverResult.Active) return;
        Result = result;
        _credential.Dispose();
    }

    public void Dispose() => Finish(CoverResult.Closed);
}
