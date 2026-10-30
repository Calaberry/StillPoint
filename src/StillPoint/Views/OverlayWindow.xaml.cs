using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using StillPoint.Interop;
using StillPoint.Models;
using StillPoint.Services;

namespace StillPoint.Views;

/// <summary>
/// Borderless, fully transparent, click-through, always-on-top window that carries the 16 dots.
/// It never takes focus (WS_EX_NOACTIVATE) and never swallows mouse input (WS_EX_TRANSPARENT), so it
/// behaves like a sheet of glass laid over the game.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _watchdog;
    private IntPtr _hwnd;
    private IntPtr _lastForeground;
    private DisplayInfo? _display;

    public OverlayWindow(OverlaySettings settings)
    {
        Settings = settings;
        InitializeComponent();
        Surface.Settings = settings;

        _watchdog = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1.2),
        };
        _watchdog.Tick += OnWatchdogTick;

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public OverlaySettings Settings { get; }

    /// <summary>
    /// Optional window handle the overlay should sit *below* (the settings window). Keeping the dots
    /// underneath our own UI avoids white specks floating over the panel, without ever activating or
    /// stealing focus from the game.
    /// </summary>
    public Func<IntPtr>? AnchorProvider { get; set; }

    /// <summary>The display currently covered, for the status readout in the settings window.</summary>
    public DisplayInfo? CoveredDisplay => _display;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeOverlayWindow(_hwnd);
        Log.Info($"Overlay HWND created: 0x{_hwnd.ToInt64():X}");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOverDisplay();
        _watchdog.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _watchdog.Stop();
        _watchdog.Tick -= OnWatchdogTick;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Log.Info("Overlay window closed.");
    }

    /// <summary>Places the window exactly over the configured display, in physical pixels.</summary>
    public void PositionOverDisplay()
    {
        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        }

        DisplayInfo display = MonitorService.ResolveDisplay(Settings.MonitorDevice);
        _display = display;

        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0.01)
        {
            scale = 1.0;
        }

        // Keep WPF's own bookkeeping in sync, then hand the exact physical rectangle to Win32.
        Left = display.Left / scale;
        Top = display.Top / scale;
        Width = display.Width / scale;
        Height = display.Height / scale;

        NativeMethods.SetWindowPos(
            _hwnd,
            ResolveInsertAfter(),
            display.Left,
            display.Top,
            display.Width,
            display.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT actual))
        {
            bool ok = actual.Left == display.Left && actual.Top == display.Top
                      && actual.Width == display.Width && actual.Height == display.Height;
            Log.Info(ok
                ? $"Overlay positioned over {display.DeviceName} {display.Width}x{display.Height} (dpi scale {scale:0.##})."
                : $"Overlay position mismatch: asked {display.Left},{display.Top} {display.Width}x{display.Height}, got {actual}.");
        }
    }

    private void OnWatchdogTick(object? sender, EventArgs e)
    {
        if (!IsVisible || _hwnd == IntPtr.Zero)
        {
            return;
        }

        DisplayInfo display = _display ?? MonitorService.ResolveDisplay(Settings.MonitorDevice);
        if (NativeMethods.GetWindowRect(_hwnd, out NativeMethods.RECT rect)
            && (rect.Left != display.Left || rect.Top != display.Top
                || rect.Width != display.Width || rect.Height != display.Height))
        {
            // Resolution / display change or a game that resized the desktop: re-cover the display.
            PositionOverDisplay();
            return;
        }

        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground != _lastForeground)
        {
            _lastForeground = foreground;
            if (IsOwnWindow(foreground))
            {
                // Our own settings window is on screen; it must stay above the dots.
                return;
            }

            // Something above us may have changed z-order; re-assert topmost without stealing focus.
            NativeMethods.SetWindowPos(
                _hwnd,
                ResolveInsertAfter(),
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    private IntPtr ResolveInsertAfter()
    {
        IntPtr anchor = AnchorProvider?.Invoke() ?? IntPtr.Zero;
        return anchor == IntPtr.Zero ? NativeMethods.HWND_TOPMOST : anchor;
    }

    private static bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Log.Info("Display settings changed; re-positioning overlay.");
        Dispatcher.BeginInvoke(new Action(PositionOverDisplay), DispatcherPriority.Background);
    }
}
