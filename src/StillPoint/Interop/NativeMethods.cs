using System.Runtime.InteropServices;

namespace StillPoint.Interop;

/// <summary>Win32 surface used by the overlay, the tray and the compatibility probes.</summary>
internal static class NativeMethods
{
    internal const int GWL_EXSTYLE = -20;
    internal const int GWL_STYLE = -16;

    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_EX_LAYERED = 0x00080000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_TOPMOST = 0x00000008;

    internal const int WS_POPUP = unchecked((int)0x80000000);
    internal const int WS_VISIBLE = 0x10000000;

    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);
    internal static readonly IntPtr HWND_TOP = IntPtr.Zero;

    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
    internal const uint SWP_FRAMECHANGED = 0x0020;

    internal const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom}) {Width}x{Height}";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT pt, int flags);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder name, int count);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(IntPtr hWnd);

    /// <summary>Applies the extended window styles that make the overlay click-through and non-activating.</summary>
    internal static void MakeOverlayWindow(IntPtr hwnd)
    {
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, ex);
        // Force the style change to take effect for an already realised HWND.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    internal static RECT GetMonitorBounds(IntPtr hwnd)
    {
        IntPtr mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfo(mon, ref info))
        {
            return info.rcMonitor;
        }

        GetWindowRect(hwnd, out RECT fallback);
        return fallback;
    }

    internal static (int Width, int Height) GetPrimaryMonitorSize()
    {
        IntPtr mon = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (GetMonitorInfo(mon, ref info))
        {
            return (info.rcMonitor.Width, info.rcMonitor.Height);
        }

        return (0, 0);
    }

    internal static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "(none)";
        }

        var title = new System.Text.StringBuilder(512);
        GetWindowText(hwnd, title, title.Capacity);
        var cls = new System.Text.StringBuilder(256);
        GetClassName(hwnd, cls, cls.Capacity);
        return $"{cls} \"{title}\"";
    }
}
