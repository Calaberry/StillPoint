using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ExclusiveGame
{
    /// <summary>
    /// Minimal Direct3D 11 "true exclusive fullscreen" demo.
    ///
    /// Creates a Win32 popup window covering the primary monitor, creates a
    /// windowed DXGI swap chain for that HWND, then switches it to exclusive
    /// fullscreen with IDXGISwapChain::SetFullscreenState(TRUE, NULL) - the
    /// same thing a real fullscreen-exclusive game does (DWM bypass).
    ///
    /// Rendering: an animated background colour plus a moving bar drawn as a
    /// small texture copied onto the back buffer with CopySubresourceRegion
    /// (ClearRenderTargetView in D3D11 has no sub-rectangle overload).
    ///
    /// Exit with Esc. Optional --seconds N auto-exits after N seconds
    /// (useful for scripted capture tests).
    /// </summary>
    internal static class Program
    {
        private const int VK_ESCAPE = 0x1B;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private static IntPtr _hwnd = IntPtr.Zero;
        private static string _logPath = "";

        [STAThread]
        private static int Main(string[] args)
        {
            int runSeconds = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seconds" && i + 1 < args.Length && int.TryParse(args[i + 1], out int s))
                {
                    runSeconds = s;
                }
                else if (args[i] == "--help" || args[i] == "-h")
                {
                    Console.WriteLine("ExclusiveGame [--seconds N]");
                    return 0;
                }
            }

            EnableDpiAwareness();
            _logPath = Path.Combine(Path.GetTempPath(), "stillpoint-exclusivegame.log");
            try { File.WriteAllText(_logPath, string.Empty); } catch { }

            Console.WriteLine("ExclusiveGame: Direct3D 11 exclusive fullscreen test");
            Log("start pid=" + Process.GetCurrentProcess().Id);

            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);
            Log("primary screen px=" + screenW + "x" + screenH);

            // --- Win32 window (predefined "STATIC" class: no RegisterClass needed) ---
            _hwnd = CreateWindowExW(
                0,
                "STATIC",
                "ExclusiveGame (D3D11 exclusive fullscreen)",
                WS_POPUP | WS_VISIBLE,
                0, 0, screenW, screenH,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_hwnd == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Fail("CreateWindowExW failed, GetLastError=" + err);
                return 2;
            }

            Log("hwnd=0x" + _hwnd.ToInt64().ToString("X"));
            SetForegroundWindow(_hwnd);

            // --- D3D11 device ---
            FeatureLevel[] levels =
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0,
                FeatureLevel.Level_10_1,
                FeatureLevel.Level_10_0,
            };

            Result result = D3D11.D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                levels,
                out ID3D11Device device,
                out ID3D11DeviceContext context);

            if (result.Failure)
            {
                Fail("D3D11CreateDevice failed: " + result.Description + " (0x" + result.Code.ToString("X8") + ")");
                DestroyWindowSafe();
                return 3;
            }

            Log("D3D11 device created, featureLevel=" + device.FeatureLevel);

            // --- DXGI swap chain for the HWND ---
            using IDXGIFactory2 factory = DXGI.CreateDXGIFactory2<IDXGIFactory2>(false);

            SwapChainDescription1 desc = new SwapChainDescription1
            {
                Width = (uint)screenW,
                Height = (uint)screenH,
                Format = Format.R8G8B8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.Discard,
                AlphaMode = AlphaMode.Unspecified,
                Flags = SwapChainFlags.AllowModeSwitch,
            };

            IDXGISwapChain1 swapChain;
            try
            {
                swapChain = factory.CreateSwapChainForHwnd(device, _hwnd, desc, null, null);
            }
            catch (Exception ex)
            {
                Fail("CreateSwapChainForHwnd threw: " + ex.Message);
                DestroyWindowSafe();
                return 4;
            }

            if (swapChain == null)
            {
                Fail("CreateSwapChainForHwnd returned null");
                DestroyWindowSafe();
                return 4;
            }

            // --- THE ACTUAL EXCLUSIVE FULLSCREEN CALL ---
            Result fs1 = swapChain.SetFullscreenState(true, null);
            bool exclusiveOk = fs1.Success;
            Log("SetFullscreenState(TRUE, NULL) -> " + (exclusiveOk ? "Success" : "FAILED: " + fs1.Description + " (0x" + fs1.Code.ToString("X8") + ")"));

            Result fs2 = swapChain.GetFullscreenState(out RawBool isFullscreen, out IDXGIOutput fullscreenOutput);
            bool stateRead = fs2.Success;
            bool reportedFullscreen = stateRead && (bool)isFullscreen;
            Log("GetFullscreenState -> read=" + (stateRead ? "ok" : "failed " + fs2.Description) +
                " fullscreen=" + reportedFullscreen +
                " output=" + (stateRead && fullscreenOutput != null ? "0x" + fullscreenOutput.NativePointer.ToInt64().ToString("X") : "null"));

            // Verify the window now covers the whole primary monitor physically.
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (GetMonitorInfo(MonitorFromWindow(_hwnd, 2 /* MONITOR_DEFAULTTONEAREST */), ref mi))
            {
                Log("monitor rcMonitor=(" + mi.rcMonitor.Left + "," + mi.rcMonitor.Top + ")-(" +
                    mi.rcMonitor.Right + "," + mi.rcMonitor.Bottom + ") rcWork=(" + mi.rcWork.Left + "," +
                    mi.rcWork.Top + ")-(" + mi.rcWork.Right + "," + mi.rcWork.Bottom + ")");
            }

            // --- render target + moving bar texture ---
            using ID3D11Texture2D backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
            using ID3D11RenderTargetView rtv = device.CreateRenderTargetView(backBuffer);
            context.OMSetRenderTargets(rtv);

            const int barW = 520;
            const int barH = 96;
            byte[] barPixels = MakeBarPixels(barW, barH);
            using ID3D11Texture2D barTexture = device.CreateTexture2D(
                barPixels,
                Format.R8G8B8A8_UNorm,
                (uint)barW,
                (uint)barH,
                arraySize: 1,
                mipLevels: 1,
                bindFlags: BindFlags.ShaderResource,
                miscFlags: ResourceOptionFlags.None,
                usage: ResourceUsage.Default);

            Log("swapchain size=" + screenW + "x" + screenH + " format=" + desc.Format +
                " buffers=" + desc.BufferCount + " effect=" + desc.SwapEffect);
            Log("bar texture " + barW + "x" + barH + " ready; rendering. Press Esc to exit.");

            WriteStatus("running", 0, 0, exclusiveOk, reportedFullscreen);

            // --- render loop ---
            Stopwatch clock = Stopwatch.StartNew();
            long frames = 0;
            int lastLogged = -1;
            double barX = 80, barY = 120, vx = 9.0, vy = 6.5;
            bool quit = false;

            while (!quit)
            {
                PumpMessages(ref quit);
                if (quit) break;

                if ((GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                {
                    Log("Escape pressed, exiting");
                    break;
                }

                if (runSeconds > 0 && clock.Elapsed.TotalSeconds >= runSeconds)
                {
                    Log("reached --seconds " + runSeconds + ", exiting");
                    break;
                }

                double t = clock.Elapsed.TotalSeconds;

                // animated background colour (slow hue sweep)
                float r = (float)(0.5 + 0.5 * Math.Sin(t * 0.9));
                float g = (float)(0.5 + 0.5 * Math.Sin(t * 0.9 + 2.094));
                float b = (float)(0.5 + 0.5 * Math.Sin(t * 0.9 + 4.188));
                context.ClearRenderTargetView(rtv, new Color4(r * 0.35f, g * 0.35f, b * 0.35f, 1.0f));

                // bounce the bar inside the screen
                barX += vx;
                barY += vy;
                if (barX < 0) { barX = 0; vx = -vx; }
                if (barY < 0) { barY = 0; vy = -vy; }
                if (barX > screenW - barW) { barX = screenW - barW; vx = -vx; }
                if (barY > screenH - barH) { barY = screenH - barH; vy = -vy; }

                context.CopySubresourceRegion(
                    backBuffer,
                    0,
                    (uint)Math.Round(barX),
                    (uint)Math.Round(barY),
                    0,
                    barTexture,
                    0);

                Result present = swapChain.Present(1, PresentFlags.None);
                if (present.Failure)
                {
                    Log("Present failed: " + present.Description + " (0x" + present.Code.ToString("X8") + ") at frame " + frames);
                    break;
                }

                frames++;

                if (frames % 30 == 0 && lastLogged != (int)(frames / 30))
                {
                    lastLogged = (int)(frames / 30);
                    double fps = frames / Math.Max(0.001, clock.Elapsed.TotalSeconds);
                    Log(string.Format("frames={0} fps={1:F1} bar=({2:F0},{3:F0}) fullscreen={4}",
                        frames, fps, barX, barY, reportedFullscreen));
                    WriteStatus("running", frames, fps, exclusiveOk, reportedFullscreen);
                }
            }

            // --- teardown ---
            swapChain.SetFullscreenState(false, null);
            Result fs3 = swapChain.GetFullscreenState(out RawBool stillFullscreen, out IDXGIOutput _);
            Log("teardown SetFullscreenState(FALSE, NULL); fullscreen now=" + (fs3.Success && (bool)stillFullscreen));

            double finalFps = frames / Math.Max(0.001, clock.Elapsed.TotalSeconds);
            Log(string.Format("SUMMARY frames={0} seconds={1:F2} avg_fps={2:F1} SetFullscreenState(TRUE)= {3} reported_fullscreen={4}",
                frames, clock.Elapsed.TotalSeconds, finalFps,
                exclusiveOk ? "Success" : "Failed", reportedFullscreen));

            WriteStatus("exited", frames, finalFps, exclusiveOk, reportedFullscreen);

            try { context.Dispose(); } catch { }
            try { device.Dispose(); } catch { }
            try { swapChain.Dispose(); } catch { }
            DestroyWindowSafe();
            try { DestroyWindow(_hwnd); } catch { }

            return exclusiveOk ? 0 : 5;
        }

        private static byte[] MakeBarPixels(int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    double fx = (double)x / width;
                    double fy = (double)y / height;
                    // diagonal bright stripes so movement is obvious
                    double stripe = 0.5 + 0.5 * Math.Sin((x + y * 2) * 0.09);
                    byte r = (byte)(250 - 40 * fx);
                    byte g = (byte)(60 + 170 * stripe * (1.0 - 0.5 * fy));
                    byte b = (byte)(30 + 90 * fx);
                    pixels[i + 0] = r;
                    pixels[i + 1] = g;
                    pixels[i + 2] = b;
                    pixels[i + 3] = 255;
                }
            }
            return pixels;
        }

        private static void PumpMessages(ref bool quit)
        {
            while (PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                if (msg.message == WM_QUIT)
                {
                    quit = true;
                    return;
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        private static void EnableDpiAwareness()
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } // PER_MONITOR_AWARE_V2
            catch { }
        }

        private static void Log(string line)
        {
            string s = "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " + line;
            Console.WriteLine(s);
            try { Console.Out.Flush(); } catch { }
            try { File.AppendAllText(_logPath, s + Environment.NewLine); } catch { }
        }

        private static void WriteStatus(string note, long frames, double fps, bool exclusiveOk, bool reportedFullscreen)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "stillpoint-exclusivegame.status.txt");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("note=" + note);
                sb.AppendLine("process_id=" + Process.GetCurrentProcess().Id);
                sb.AppendLine("frames=" + frames);
                sb.AppendLine("fps=" + fps.ToString("F1"));
                sb.AppendLine("set_fullscreen_success=" + exclusiveOk);
                sb.AppendLine("reported_fullscreen=" + reportedFullscreen);
                sb.AppendLine("log_path=" + _logPath);
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
        }

        private static void Fail(string message)
        {
            Console.Error.WriteLine("ERROR: " + message);
            try { Console.Error.Flush(); } catch { }
            try { File.AppendAllText(_logPath, "ERROR: " + message + Environment.NewLine); } catch { }
        }

        private static void DestroyWindowSafe()
        {
            if (_hwnd != IntPtr.Zero)
            {
                try { DestroyWindow(_hwnd); } catch { }
                _hwnd = IntPtr.Zero;
            }
        }

        // ---------------- Win32 ----------------

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint PM_REMOVE = 0x0001;
        private const uint WM_QUIT = 0x0012;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiAwarenessContext);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    }
}
