using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AiGuardLock;

internal readonly record struct MonitorBounds(int Left, int Top, int Right, int Bottom, bool IsPrimary);

internal static class DisplayMonitors
{
    internal static IReadOnlyList<MonitorBounds> GetAll()
    {
        var monitors = new List<MonitorBounds>();
        var error = 0;
        MonitorCallback callback = (nint monitor, nint dc, ref NativeRect rect, nint data) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }
            monitors.Add(new(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right,
                info.Monitor.Bottom, (info.Flags & 1) != 0));
            return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0) || error != 0)
            throw new Win32Exception(error == 0 ? Marshal.GetLastWin32Error() : error);
        if (monitors.Count == 0 || monitors.Count(m => m.IsPrimary) != 1)
            throw new InvalidOperationException("Could not determine the primary monitor.");
        return monitors;
    }

    internal static void Place(Window window, MonitorBounds bounds)
    {
        if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top)
            throw new ArgumentException("Invalid monitor bounds.", nameof(bounds));
        var handle = new WindowInteropHelper(window).EnsureHandle();
        // Enumeration and positioning use the same thread DPI context and native coordinates.
        if (!SetWindowPos(handle, new nint(-1), bounds.Left, bounds.Top,
            bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0010)) // SWP_NOACTIVATE
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
