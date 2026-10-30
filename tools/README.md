# StillPoint test tools

Helper tools used to verify that the StillPoint overlay really sits on top of a
"game" and that mouse input passes through it. Everything here is standalone:
nothing in this folder is referenced by the StillPoint app.

| Tool | What it is |
| --- | --- |
| `FakeGame\` | WPF app that behaves like a **borderless fullscreen game** (a normal window covering the primary monitor exactly, not topmost, dark animated scene, live frame counter, two clickable fake buttons with on-screen click counters). |
| `ExclusiveGame\` | Console app that creates a real Direct3D 11 **exclusive fullscreen** swap chain (`SetFullscreenState(TRUE, NULL)`, DWM bypassed) and animates a solid colour plus a moving striped bar. |
| `capture.ps1` | DPI-aware screenshot of the primary screen (or the whole virtual desktop) to PNG. |
| `clicktest.ps1` | Moves the cursor with `SendInput` and issues a real left click; prints the window under the point and the foreground window before/after. |

---

## Prerequisites / environment

There is **no system-wide .NET 8 SDK** on this machine; a local SDK lives at:

```
D:\dsh file\StillPoint\.dotnet\dotnet.exe     (8.0.427)
```

Always call it by full path. Set these once per shell:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = 'D:\dsh file\StillPoint\.dotnet\dotnet.exe'
```

The machine-wide runtime (`C:\Program Files\dotnet`) only has .NET 6/7, so the
built `.exe` apphosts need `DOTNET_ROOT` pointed at the local SDK (or you can
launch the `.dll` through the local `dotnet.exe`). Either works:

```powershell
$env:DOTNET_ROOT     = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_ROOT_X64 = 'D:\dsh file\StillPoint\.dotnet'
```

Build commands (from anywhere):

```powershell
& $dotnet build 'D:\dsh file\StillPoint\tools\FakeGame\FakeGame.csproj'       -c Release
& $dotnet build 'D:\dsh file\StillPoint\tools\ExclusiveGame\ExclusiveGame.csproj' -c Release
```

Both build with 0 warnings / 0 errors.

---

## 1. FakeGame (borderless fullscreen test scene)

Build output: `tools\FakeGame\bin\Release\net8.0-windows\FakeGame.exe`.

Default (borderless fullscreen, covers the primary monitor 0,0 - 2560x1440):

```powershell
$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
& 'D:\dsh file\StillPoint\tools\FakeGame\bin\Release\net8.0-windows\FakeGame.exe'
```

Windowed mode (a plain **1280x720 physical pixel** window, centred):

```powershell
& 'D:\dsh file\StillPoint\tools\FakeGame\bin\Release\net8.0-windows\FakeGame.exe' --windowed
```

Alternative launch without touching `DOTNET_ROOT` (shows a console window for the
host process):

```powershell
& $dotnet 'D:\dsh file\StillPoint\tools\FakeGame\bin\Release\net8.0-windows\FakeGame.dll'
```

What it shows:

* rotating dark gradient + soft blobs + moving scanline (looks like a game menu),
* big centred label `BORDERLESS FULLSCREEN TEST`,
* live `FRAME n` / `FPS x.x` counters and the real window rect in physical px,
* a bottom UI bar with `BUTTON A` / `BUTTON B` and their on-screen click counts.

Press **Esc** to close (only while it is the foreground window). `ShowInTaskbar`
is true, the window is **not** topmost - exactly what an overlay must handle.
While it is the foreground fullscreen window Windows hides the taskbar, so the
page has no visible taskbar even though the window is a normal window.

Machine-readable diagnostics (useful for scripted checks):

* `%TEMP%\stillpoint-fakegame.status.txt` - mode, `process_id`, `window_rect_px`,
  `ui_bar_rect_px`, `button_a_center_px`, `button_b_center_px`, `clicks_a`,
  `clicks_b`. Rewritten on load/click/every 2 s/close.
* `%TEMP%\stillpoint-fakegame.log` - timestamped start / click / Esc / close lines.

## 2. ExclusiveGame (true Direct3D 11 exclusive fullscreen)

NuGet packages: `Vortice.Direct3D11` 3.8.3 + `Vortice.DXGI` 3.8.3.

Build output: `tools\ExclusiveGame\bin\Release\net8.0-windows\ExclusiveGame.exe`.

```powershell
$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
& 'D:\dsh file\StillPoint\tools\ExclusiveGame\bin\Release\net8.0-windows\ExclusiveGame.exe'
```

Run for a fixed number of seconds (handy for scripted tests):

```powershell
& 'D:\dsh file\StillPoint\tools\ExclusiveGame\bin\Release\net8.0-windows\ExclusiveGame.exe' --seconds 20
```

What it does:

1. sets the process DPI awareness (so window coordinates are physical pixels),
2. creates a borderless popup window over the primary monitor,
3. creates a D3D11 device (BGRA support, highest feature level available),
4. creates a `CreateSwapChainForHwnd` swap chain (2560x1440, R8G8B8A8_UNorm,
   2 buffers, `SwapEffect.Discard`, `AllowModeSwitch`) and calls
   `IDXGISwapChain::SetFullscreenState(TRUE, NULL)`,
5. renders an animated clear colour plus a moving striped bar
   (`CopySubresourceRegion` from a small texture - `ClearRenderTargetView` has no
   sub-rect overload in these Vortice bindings),
6. logs `frames=... fps=... bar=... fullscreen=...` every 30 frames,
7. restores windowed state on exit.

Exit code 0 only when `SetFullscreenState(TRUE)` succeeded (5 if the fullscreen
switch failed, 2/3/4 for window/device/swap-chain failures). **Esc** exits early.
It also writes `%TEMP%\stillpoint-exclusivegame.log` and
`...\stillpoint-exclusivegame.status.txt`.

Note: `Present(1, 0)` is vsync-locked, so on this 165 Hz panel it reports
~148-165 fps. A console window of the host is visible (it is a console app).

## 3. capture.ps1

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\dsh file\StillPoint\tools\capture.ps1' -Out 'C:\Temp\shot.png'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\dsh file\StillPoint\tools\capture.ps1' -Out 'C:\Temp\shot.png' -AllScreens
```

* `-Out <path.png>` (mandatory) - PNG path; parent folder is created.
* `-AllScreens` - capture the whole virtual desktop instead of the primary screen.

The script sets Per-Monitor-V2 DPI awareness on its process and thread before
touching GDI, so the PNG is in real physical pixels (2560x1440 on this machine,
not the 2048x1152 a DPI-unaware process reports). It does not steal focus and
works while another app is in the foreground.

Prints: `CAPTURED <abs path>`, `IMAGE_SIZE WxH px`, `BOUNDS x=.. y=..`,
`DPI <n> dpi (scale ..%)`, `FILE <bytes> bytes`.

Because of the machine's execution policy, call it via `powershell.exe ... -File`
instead of `& '.\capture.ps1'` (dot-sourcing the file is blocked:
"running scripts is disabled on this system").

## 4. clicktest.ps1

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\dsh file\StillPoint\tools\clicktest.ps1' -X 173 -Y 1312
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\dsh file\StillPoint\tools\clicktest.ps1' -X 173 -Y 1312 -Clicks 2
```

* `-X`, `-Y` (mandatory) - target point in **physical** pixels.
* `-Clicks` - number of left clicks (default 1).

It uses `SendInput` (`MOUSEEVENTF_MOVE | LEFTDOWN | LEFTUP | ABSOLUTE |
VIRTUALDESK`) so the cursor really moves and a real click is injected; the cursor
stays at the target afterwards. Before and after it prints
`WindowFromPoint` (hwnd, class, title, pid, process, ancestor root) and the
foreground window, plus cursor position and `SendInput_ok`. If `SendInput`
returns fewer events than requested the script throws.

---

## Verified on this machine (2026-10-30)

Environment: Windows 11 10.0.22631, single display **2560x1440 @ 125% (120 dpi)**,
NVIDIA RTX 4060 Laptop GPU, taskbar 60 px at the bottom (work area bottom = 1380).

1. FakeGame fullscreen + capture: `capture.ps1` produced a 2560x1440 PNG showing
   the whole test scene (`BORDERLESS FULLSCREEN TEST`, `FRAME 1003`, `FPS 165.1`,
   `RECT 0,0,2560x1440`, both fake buttons and counters). Taskbar is auto-hidden
   while FakeGame is foreground.
2. ExclusiveGame + capture: `SetFullscreenState(TRUE, NULL)` returned Success,
   `frames=2968` over 20.06 s (avg 148 fps), fullscreen state reported True, and
   `capture.ps1` did capture the rendered frame (solid maroon background plus the
   moving striped bar). GDI screen capture working during exclusive fullscreen is
   driver/OS dependent - treat "black image" as a normal possible result elsewhere.
3. clicktest vs FakeGame: clicking `button_a_center_px=173,1312` twice and
   `button_b_center_px=594,1312` once produced on-screen `A clicks: 2` /
   `B clicks: 1`, the same values in `status.txt`, and `CLICK button=A count=..`
   lines in the log. `WindowFromPoint` and the foreground window both resolved to
   FakeGame's `HwndWrapper[FakeGame;;...]` window.
4. Esc closes FakeGame (both modes) and ExclusiveGame, writing clean
   `closing`/`closed` lines and leaving no process behind.

## Caveats / gotchas hit while building this

* **Real display is 2560x1440 @ 125%.** A DPI-unaware process sees 2048x1152, so
  any coordinate math must be done in physical pixels (both scripts set DPI
  awareness; FakeGame's manifest is PerMonitorV2).
* **WPF fails in globalization-invariant mode** on this SDK
  (`MS.Internal.FontCache.MajorLanguages` -> `CultureNotFoundException`), so
  `<InvariantGlobalization>` must stay out of both `.csproj` files.
* Machine-wide .NET is 6/7 only; the local 8.0 SDK must be used for build and
  `DOTNET_ROOT` must point at it for the apphost `.exe`s.
* **Third-party topmost overlay is running on this desktop** (a game-accelerator
  "FPS/GPU/CPU" overlay and a toast in the bottom-right corner). It draws above
  fullscreen apps and shows up in screenshots - not part of these tools.
* Exclusive fullscreen switches the monitor mode; the desktop is restored on a
  clean exit (`SetFullscreenState(FALSE)`).
* `tools\*` contains `bin\`/`obj\` build output; those are not sources.
