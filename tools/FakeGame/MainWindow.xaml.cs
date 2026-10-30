using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace FakeGame
{
    /// <summary>
    /// Borderless "fullscreen game" test window.
    ///
    /// Default: borderless window covering the primary monitor exactly, NOT topmost,
    /// dark animated scene, big centred label, live frame counter and two fake buttons
    /// whose click counts are shown on screen (and in a log file + status file).
    ///
    /// --windowed : start as a normal 1280x720 window instead.
    /// Esc        : close.
    ///
    /// Diagnostics (so tests can be machine-verified):
    ///   %TEMP%\stillpoint-fakegame.log        - timestamped events (start, click, close)
    ///   %TEMP%\stillpoint-fakegame.status.txt - current window rect + button centres (physical px)
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string LogFileName = "stillpoint-fakegame.log";
        private const string StatusFileName = "stillpoint-fakegame.status.txt";

        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int VK_ESCAPE = 0x1B;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private readonly string _logPath = Path.Combine(Path.GetTempPath(), LogFileName);
        private readonly string _statusPath = Path.Combine(Path.GetTempPath(), StatusFileName);
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private bool _windowed;
        private long _frames;
        private long _clickA;
        private long _clickB;
        private long _fpsFrameBase;
        private double _fpsTimeBase;
        private double _fps = 60.0;
        private bool _closing;
        private DispatcherTimer? _escTimer;
        private DispatcherTimer? _statusTimer;

        public MainWindow()
        {
            InitializeComponent();

            string[] args = Environment.GetCommandLineArgs();
            _windowed = Array.Exists(args, a => string.Equals(a, "--windowed", StringComparison.OrdinalIgnoreCase));

            if (_windowed)
            {
                // Brief says "a normal 1280x720 window". Keep 1280x720 in *physical*
                // pixels so screenshots/clicktest coordinates match: WPF sizes in DIPs.
                double dipScale = 1.0;
                int screenCx = GetSystemMetrics(0);
                if (screenCx > 0 && SystemParameters.PrimaryScreenWidth > 0)
                {
                    dipScale = screenCx / SystemParameters.PrimaryScreenWidth;
                }

                Width = 1280.0 / dipScale;
                Height = 720.0 / dipScale;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Title = "FakeGame (windowed) — borderless fullscreen test";
            }
            else
            {
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = 0;
                Top = 0;
                Title = "FakeGame — borderless fullscreen test";
            }

            ModeText.Text = _windowed ? "MODE: windowed 1280x720" : "MODE: borderless fullscreen";

            // Start clean: remove stale status/log from a previous run.
            TrySetStatusText("# FakeGame status - not running yet");
            TryAppendLog("=== FakeGame starting (mode=" + (_windowed ? "windowed" : "fullscreen") + ") ===");
        }

        // ---------------------------------------------------------------- lifecycle

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyPlacement();
            Activate();
            Focus();

            CompositionTarget.Rendering += OnRendering;

            _escTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _escTimer.Tick += (_, __) =>
            {
                // Escape only when this app is the foreground window, so Esc pressed in
                // another application cannot kill the test scene.
                if (!_closing && GetForegroundWindow() == new WindowInteropHelper(this).Handle &&
                    (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                {
                    AppLog("Esc pressed (foreground poll) -> closing");
                    Close();
                }
            };
            _escTimer.Start();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _statusTimer.Tick += (_, __) =>
            {
                WriteStatus("periodic");
                UpdateStatusPanel();
            };
            _statusTimer.Start();

            WriteStatus("loaded");

            PresentationSource? src = PresentationSource.FromVisual(this);
            double scaleX = 1.0, scaleY = 1.0;
            if (src?.CompositionTarget != null)
            {
                scaleX = src.CompositionTarget.TransformToDevice.M11;
                scaleY = src.CompositionTarget.TransformToDevice.M22;
            }

            AppLog(string.Format(CultureInfo.InvariantCulture,
                "loaded: windowed={0} dpiScale={1:0.###} primaryDip={2:0.#}x{3:0.#} primaryPx={4}x{5} workAreaDip={6}",
                _windowed, scaleX, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight,
                GetSystemMetrics(0), GetSystemMetrics(1), SystemParameters.WorkArea));

            if (!_windowed)
            {
                // Try to be the foreground window so the shell treats us like a running game.
                Activate();
            }
        }

        private void ApplyPlacement()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            if (_windowed)
            {
                // Force the OS window rect to 1280x720 *physical* pixels, centred.
                int winCx = GetSystemMetrics(0); // SM_CXSCREEN
                int winCy = GetSystemMetrics(1); // SM_CYSCREEN
                if (winCx >= 1280 && winCy >= 720)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, (winCx - 1280) / 2, (winCy - 720) / 2, 1280, 720,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }

                UiBar.Margin = new Thickness(28, 0, 28, 28);
                UpdateLayout();
                WriteStatus("placement-windowed");
                UpdateStatusPanel();
                return;
            }

            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;

            // Force the OS window rect to be exactly the monitor rect in physical pixels.
            int cx = GetSystemMetrics(0); // SM_CXSCREEN
            int cy = GetSystemMetrics(1); // SM_CYSCREEN
            if (cx > 0 && cy > 0)
            {
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE);
            }

            UpdateLayout();

            // Keep the fake menu bar clear of the taskbar (the window is not topmost, so the
            // taskbar can draw on top of the bottom of the window).
            double taskbarDip = Math.Max(0.0, SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Bottom);
            UiBar.Margin = new Thickness(28, 0, 28, taskbarDip + 16);
            UpdateLayout();

            WriteStatus("placement-fullscreen");
            UpdateStatusPanel();
        }

        private void UpdateStatusPanel()
        {
            try
            {
                RectText.Text = "RECT " + WindowRectPx();
            }
            catch
            {
                // cosmetic only
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _closing = true;
            AppLog("closing (clicks A=" + _clickA + " B=" + _clickB + ")");
            CompositionTarget.Rendering -= OnRendering;
            _escTimer?.Stop();
            _statusTimer?.Stop();
            WriteStatus("closed");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                AppLog("Esc pressed (key handler) -> closing");
                e.Handled = true;
                Close();
            }
        }

        // ---------------------------------------------------------------- animation

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_closing)
            {
                return;
            }

            _frames++;
            double t = _clock.Elapsed.TotalSeconds;
            double w = ActualWidth;
            double h = ActualHeight;

            if (w < 2 || h < 2)
            {
                return;
            }

            // Rotating backdrop.
            GradientRotate.Angle = (t * 8.0) % 360.0;

            // Lissajous blob motion.
            Blob1Transform.X = w * 0.28 + Math.Cos(t * 0.20) * w * 0.18;
            Blob1Transform.Y = h * 0.30 + Math.Sin(t * 0.27) * h * 0.15;
            Blob2Transform.X = w * 0.74 + Math.Sin(t * 0.17 + 1.2) * w * 0.15;
            Blob2Transform.Y = h * 0.62 + Math.Cos(t * 0.23 + 0.4) * h * 0.18;
            Blob3Transform.X = w * 0.50 + Math.Cos(t * 0.13 + 2.1) * w * 0.32;
            Blob3Transform.Y = h * 0.80 + Math.Sin(t * 0.19 + 1.7) * h * 0.12;

            // Moving scanline.
            ScanlineTransform.Y = ((t * 110.0) % (h + 240.0)) - 240.0;

            // Live counters.
            FrameText.Text = "FRAME " + _frames.ToString(CultureInfo.InvariantCulture);

            if (t - _fpsTimeBase >= 0.5)
            {
                double elapsed = t - _fpsTimeBase;
                _fps = (double)(_frames - _fpsFrameBase) / elapsed;
                _fpsFrameBase = _frames;
                _fpsTimeBase = t;
                FpsText.Text = "FPS " + _fps.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        // ---------------------------------------------------------------- clicks

        private void FakeButton_Click(object sender, RoutedEventArgs e)
        {
            string which;
            long count;
            if (ReferenceEquals(sender, ButtonB))
            {
                _clickB++;
                count = _clickB;
                which = "B";
                CountB.Text = "B clicks: " + count.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                _clickA++;
                count = _clickA;
                which = "A";
                CountA.Text = "A clicks: " + count.ToString(CultureInfo.InvariantCulture);
            }

            AppLog("CLICK button=" + which + " count=" + count.ToString(CultureInfo.InvariantCulture));
            WriteStatus("click-" + which);
        }

        // ---------------------------------------------------------------- diagnostics

        private void AppLog(string message)
        {
            TryAppendLog(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message);
        }

        private void TryAppendLog(string line)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // logging is best-effort only
            }
        }

        private void TrySetStatusText(string text)
        {
            try
            {
                File.WriteAllText(_statusPath, text + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // best-effort
            }
        }

        private void WriteStatus(string note)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# FakeGame status (physical pixels)");
                sb.AppendLine("note=" + note);
                sb.AppendLine("updated=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                sb.AppendLine("mode=" + (_windowed ? "windowed" : "fullscreen"));
                sb.AppendLine("process_id=" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("primary_screen_px=" + GetSystemMetrics(0).ToString(CultureInfo.InvariantCulture) + "x" +
                              GetSystemMetrics(1).ToString(CultureInfo.InvariantCulture));

                PresentationSource? src = PresentationSource.FromVisual(this);
                if (src?.CompositionTarget != null)
                {
                    sb.AppendLine("dpi_scale=" + src.CompositionTarget.TransformToDevice.M11.ToString("0.####", CultureInfo.InvariantCulture));
                }

                sb.AppendLine("window_rect_px=" + WindowRectPx());
                sb.AppendLine("ui_bar_rect_px=" + ElementRectPx(UiBar));
                sb.AppendLine("button_a_center_px=" + ElementCenterPx(ButtonA));
                sb.AppendLine("button_b_center_px=" + ElementCenterPx(ButtonB));
                sb.AppendLine("clicks_a=" + _clickA.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("clicks_b=" + _clickB.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("log_path=" + _logPath);

                File.WriteAllText(_statusPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // best-effort
            }
        }

        private string WindowRectPx()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT r))
            {
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}x{3}",
                    r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }

            return "unknown";
        }

        private static string ElementRectPx(FrameworkElement element)
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                return "unknown";
            }

            try
            {
                Point topLeft = element.PointToScreen(new Point(0, 0));
                Point bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}x{3}",
                    (int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y),
                    (int)Math.Round(bottomRight.X - topLeft.X), (int)Math.Round(bottomRight.Y - topLeft.Y));
            }
            catch
            {
                return "unknown";
            }
        }

        private static string ElementCenterPx(FrameworkElement element)
        {
            try
            {
                Point c = element.PointToScreen(new Point(element.ActualWidth / 2.0, element.ActualHeight / 2.0));
                return string.Format(CultureInfo.InvariantCulture, "{0},{1}",
                    (int)Math.Round(c.X), (int)Math.Round(c.Y));
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
