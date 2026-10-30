<#
.SYNOPSIS
    Moves the mouse and issues real left clicks at a screen point, reporting
    which windows are there (for StillPoint overlay click-through testing).

.DESCRIPTION
    Uses Win32 SendInput with MOUSEEVENTF_MOVE|ABSOLUTE|VIRTUALDESK and
    MOUSEEVENTF_LEFTDOWN/LEFTUP, i.e. the exact input path a physical mouse
    uses, so it exercises Windows' real hit-testing (overlays with
    WS_EX_TRANSPARENT / WM_NCHITTEST=HTTRANSPARENT let the click fall through).

    Before and after the click(s) the script prints, for the target point and
    for the current foreground window:
        hwnd, class, title, pid, process name, and (for the point) the root
        window that owns it.

    The hosting process is made PER_MONITOR_AWARE_V2 first so -X/-Y are real
    physical pixels and match capture.ps1's output pixel coordinates.

    The click is NOT synthesized by moving/activating a window: moving the
    cursor this way does not change the foreground window; clicking may, which
    is expected (and is printed).

.PARAMETER X
    Screen X coordinate in physical pixels (0..2559 on the test machine).

.PARAMETER Y
    Screen Y coordinate in physical pixels (0..1439 on the test machine).

.PARAMETER Clicks
    Number of left clicks to send. Default 1.

.EXAMPLE
    .\clicktest.ps1 -X 640 -Y 720

.EXAMPLE
    .\clicktest.ps1 -X 640 -Y 720 -Clicks 3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$X,

    [Parameter(Mandatory = $true)]
    [int]$Y,

    [int]$Clicks = 1
)

$ErrorActionPreference = 'Stop'

if ($Clicks -lt 1) { $Clicks = 1 }

if (-not ('StillPoint.ClickNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StillPoint
{
    public static class ClickNative
    {
        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const uint GA_ROOT = 2;

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiAwarenessContext);

        [DllImport("user32.dll")]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiAwarenessContext);

        [DllImport("user32.dll")]
        public static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr dpiAwarenessContext);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public static string DescribeWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return "hwnd=NULL";
            }

            StringBuilder cls = new StringBuilder(256);
            GetClassName(hwnd, cls, cls.Capacity);
            StringBuilder title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);

            uint pid = 0;
            GetWindowThreadProcessId(hwnd, out pid);
            string exe = "?";
            if (pid != 0)
            {
                try
                {
                    exe = Process.GetProcessById((int)pid).ProcessName;
                }
                catch (Exception)
                {
                    exe = "<exited pid " + pid + ">";
                }
            }

            return "hwnd=0x" + hwnd.ToInt64().ToString("X") +
                   " class=\"" + cls.ToString() + "\"" +
                   " title=\"" + title.ToString() + "\"" +
                   " pid=" + pid.ToString() +
                   " exe=" + exe;
        }

        public static string WindowAt(int x, int y)
        {
            POINT p;
            p.X = x;
            p.Y = y;
            IntPtr hwnd = WindowFromPoint(p);
            string s = DescribeWindow(hwnd);
            IntPtr root = hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GA_ROOT);
            if (root != IntPtr.Zero && root != hwnd)
            {
                s += "  root=" + DescribeWindow(root);
            }
            return s;
        }

        public static string ForegroundWindow()
        {
            return DescribeWindow(GetForegroundWindow());
        }

        public static string CursorPosition()
        {
            POINT p;
            if (!GetCursorPos(out p))
            {
                return "<unknown>";
            }
            return "x=" + p.X.ToString() + " y=" + p.Y.ToString();
        }

        public static string VirtualScreenBounds()
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            return "x=" + x + " y=" + y + " w=" + w + " h=" + h;
        }

        public static string DpiInfo()
        {
            IntPtr ctx = GetThreadDpiAwarenessContext();
            int awareness = GetAwarenessFromDpiAwarenessContext(ctx);
            return "thread_awareness=" + awareness;
        }

        private static int Normalize(int value, int origin, int extent)
        {
            if (extent <= 1)
            {
                return 0;
            }
            double v = ((double)(value - origin) * 65535.0) / (double)(extent - 1);
            if (v < 0.0) v = 0.0;
            if (v > 65535.0) v = 65535.0;
            return (int)Math.Round(v);
        }

        private static INPUT MakeMouse(int nx, int ny, uint flags)
        {
            INPUT input = new INPUT();
            input.type = INPUT_MOUSE;
            input.mi.dx = nx;
            input.mi.dy = ny;
            input.mi.mouseData = 0;
            input.mi.dwFlags = flags | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
            input.mi.time = 0;
            input.mi.dwExtraInfo = IntPtr.Zero;
            return input;
        }

        private static bool Send(INPUT[] inputs)
        {
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return sent == (uint)inputs.Length;
        }

        public static bool MoveTo(int x, int y)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            int nx = Normalize(x, vx, vw);
            int ny = Normalize(y, vy, vh);

            INPUT[] inputs = new INPUT[1];
            inputs[0] = MakeMouse(nx, ny, MOUSEEVENTF_MOVE);
            return Send(inputs);
        }

        public static bool ClickAt(int x, int y)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            int nx = Normalize(x, vx, vw);
            int ny = Normalize(y, vy, vh);

            INPUT[] inputs = new INPUT[3];
            inputs[0] = MakeMouse(nx, ny, MOUSEEVENTF_MOVE);
            inputs[1] = MakeMouse(nx, ny, MOUSEEVENTF_LEFTDOWN);
            inputs[2] = MakeMouse(nx, ny, MOUSEEVENTF_LEFTUP);
            return Send(inputs);
        }
    }
}
'@
}

$processAware = [StillPoint.ClickNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$threadAware = [StillPoint.ClickNative]::SetThreadDpiAwarenessContext([IntPtr](-4))
$awareness = [StillPoint.ClickNative]::GetAwarenessFromDpiAwarenessContext(
    [StillPoint.ClickNative]::GetThreadDpiAwarenessContext())
if ($awareness -eq 0) {
    throw "StillPoint clicktest.ps1: could not enable DPI awareness (process=$processAware thread=$threadAware); coordinates would be virtualized."
}

Write-Output ("TARGET          X={0} Y={1} clicks={2}" -f $X, $Y, $Clicks)
Write-Output ("DPI             {0}, virtual_screen {1}" -f [StillPoint.ClickNative]::DpiInfo(), [StillPoint.ClickNative]::VirtualScreenBounds())
Write-Output ("CURSOR_BEFORE   {0}" -f [StillPoint.ClickNative]::CursorPosition())
Write-Output ("POINT_BEFORE    {0}" -f [StillPoint.ClickNative]::WindowAt($X, $Y))
Write-Output ("FOREGROUND_BEFORE {0}" -f [StillPoint.ClickNative]::ForegroundWindow())

$moveOk = [StillPoint.ClickNative]::MoveTo($X, $Y)
Start-Sleep -Milliseconds 150
Write-Output ("MOVE            ok={0} cursor={1}" -f $moveOk, [StillPoint.ClickNative]::CursorPosition())
Write-Output ("POINT_AFTER_MOVE {0}" -f [StillPoint.ClickNative]::WindowAt($X, $Y))

for ($i = 1; $i -le $Clicks; $i++) {
    $clickOk = [StillPoint.ClickNative]::ClickAt($X, $Y)
    Write-Output ("CLICK           {0}/{1} SendInput_ok={2}" -f $i, $Clicks, $clickOk)
    if (-not $clickOk) {
        throw ("StillPoint clicktest.ps1: SendInput failed at click {0} [UIPI may block input to an elevated window]" -f $i)
    }
    Start-Sleep -Milliseconds 120
}

Start-Sleep -Milliseconds 120
Write-Output ("CURSOR_AFTER    {0}" -f [StillPoint.ClickNative]::CursorPosition())
Write-Output ("POINT_AFTER     {0}" -f [StillPoint.ClickNative]::WindowAt($X, $Y))
Write-Output ("FOREGROUND_AFTER {0}" -f [StillPoint.ClickNative]::ForegroundWindow())
