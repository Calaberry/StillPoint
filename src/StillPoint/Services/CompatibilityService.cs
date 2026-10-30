using System.Diagnostics;
using System.Text;
using StillPoint.Interop;

namespace StillPoint.Services;

/// <summary>Snapshot of the foreground window, used for the compatibility panel and the test report.</summary>
public sealed record ForegroundInfo(string Title, string ProcessName, string ClassName, string Mode, bool CoversMonitor)
{
    public string Display
    {
        get
        {
            if (string.Equals(Mode, "桌面", StringComparison.Ordinal))
            {
                return "桌面";
            }

            if (string.Equals(Mode, "本程序", StringComparison.Ordinal))
            {
                return "StillPoint（本程序）";
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                return string.IsNullOrWhiteSpace(ProcessName) ? "未知窗口" : ProcessName;
            }

            if (string.IsNullOrWhiteSpace(ProcessName) ||
                Title.StartsWith(ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return Title;
            }

            return $"{Title}（{ProcessName}）";
        }
    }
}

/// <summary>
/// Read-only observations about the desktop. It never touches the game: everything comes from the
/// public Win32 window enumeration API.
/// </summary>
public static class CompatibilityService
{
    public static ForegroundInfo DescribeForeground()
    {
        try
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return new ForegroundInfo(string.Empty, string.Empty, string.Empty, "未知", false);
            }

            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(hwnd, title, title.Capacity);
            var className = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, className, className.Capacity);

            string processName = string.Empty;
            try
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid != 0)
                {
                    using Process process = Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                }
            }
            catch
            {
                // Access denied for elevated processes; the rest of the information is still useful.
            }

            bool covers = CoversMonitor(hwnd);
            return new ForegroundInfo(
                title.ToString(),
                processName,
                className.ToString(),
                ModeOf(hwnd, covers, processName, className.ToString()),
                covers);
        }
        catch (Exception exception)
        {
            Log.Warn($"Foreground inspection failed: {exception.Message}");
            return new ForegroundInfo(string.Empty, string.Empty, string.Empty, "未知", false);
        }
    }

    private static string ModeOf(IntPtr hwnd, bool covers, string processName, string className)
    {
        if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase))
        {
            return "桌面";
        }

        if (string.Equals(processName, "StillPoint", StringComparison.OrdinalIgnoreCase))
        {
            return "本程序";
        }

        if (!covers)
        {
            return "窗口模式";
        }

        return "全屏（无边框）";
    }

    /// <summary>True when the window rectangle matches its monitor rectangle (borderless fullscreen or exclusive).</summary>
    private static bool CoversMonitor(IntPtr hwnd)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT windowRect))
        {
            return false;
        }

        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };

        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return Math.Abs(windowRect.Left - info.rcMonitor.Left) <= 2
               && Math.Abs(windowRect.Top - info.rcMonitor.Top) <= 2
               && Math.Abs(windowRect.Right - info.rcMonitor.Right) <= 2
               && Math.Abs(windowRect.Bottom - info.rcMonitor.Bottom) <= 2;
    }
}
