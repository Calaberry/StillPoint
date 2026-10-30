using System.Runtime.InteropServices;
using StillPoint.Interop;

namespace StillPoint.Services;

/// <summary>Physical bounds and device name of one display.</summary>
public sealed record DisplayInfo(string DeviceName, int Left, int Top, int Width, int Height, bool IsPrimary)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public string FriendlyName
    {
        get
        {
            string label = DeviceName.Replace(@"\\.\", string.Empty);
            return IsPrimary ? $"{label} · 主显示器 · {Width}×{Height}" : $"{label} · {Width}×{Height}";
        }
    }
}

/// <summary>Enumerates displays in raw physical pixels (no DPI scaling applied).</summary>
public static class MonitorService
{
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public NativeMethods.RECT rcMonitor;
        public NativeMethods.RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfoEx(IntPtr hMonitor, ref MONITORINFOEX info);

    public static IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var list = new List<DisplayInfo>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data) =>
            {
                try
                {
                    var info = new MONITORINFOEX
                    {
                        cbSize = Marshal.SizeOf<MONITORINFOEX>(),
                        szDevice = string.Empty,
                    };
                    if (GetMonitorInfoEx(hMonitor, ref info))
                    {
                        list.Add(new DisplayInfo(
                            info.szDevice ?? string.Empty,
                            info.rcMonitor.Left,
                            info.rcMonitor.Top,
                            info.rcMonitor.Width,
                            info.rcMonitor.Height,
                            (info.dwFlags & 1) != 0));
                    }
                    else
                    {
                        list.Add(new DisplayInfo(
                            string.Empty,
                            rect.Left,
                            rect.Top,
                            rect.Right - rect.Left,
                            rect.Bottom - rect.Top,
                            list.Count == 0));
                    }
                }
                catch (Exception exception)
                {
                    // An exception must never escape a native callback.
                    Log.Warn($"Monitor enumeration callback failed: {exception.Message}");
                }

                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Fall through to the primary-monitor fallback below.
        }

        if (list.Count == 0)
        {
            var size = NativeMethods.GetPrimaryMonitorSize();
            list.Add(new DisplayInfo(@"\\.\DISPLAY1", 0, 0, size.Width, size.Height, true));
        }

        return list;
    }

    /// <summary>Resolves the display that should be covered; falls back to the primary display.</summary>
    public static DisplayInfo ResolveDisplay(string? deviceName)
    {
        IReadOnlyList<DisplayInfo> displays = GetDisplays();
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            DisplayInfo? match = displays.FirstOrDefault(d =>
                string.Equals(d.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return displays.FirstOrDefault(d => d.IsPrimary) ?? displays[0];
    }
}
