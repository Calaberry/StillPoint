<#
.SYNOPSIS
    Captures the screen to a PNG file for StillPoint overlay verification.

.DESCRIPTION
    Captures the primary monitor, or the whole virtual desktop with
    -AllScreens. Uses System.Drawing
    Graphics.CopyFromScreen (GDI BitBlt), which does NOT steal focus from a
    foreground app.

    The script explicitly makes the hosting process PER_MONITOR_AWARE_V2 before
    touching GDI, so the returned image is in real physical pixels (2560x1440
    on the test machine, not the 2048x1152 "DPI-virtualized" size an unaware
    process would see) and matches clicktest.ps1's coordinate space exactly.

.PARAMETER Out
    Destination PNG path. Parent directory is created if missing; relative
    paths resolve against the current directory. An existing file is
    overwritten.

.PARAMETER AllScreens
    Capture the whole virtual desktop (all monitors, possibly starting at a
    negative origin) instead of just the primary monitor.

.EXAMPLE
    .\capture.ps1 -Out "$env:TEMP\shot.png"

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\capture.ps1 -Out C:\tmp\all.png -AllScreens
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Out,

    [switch]$AllScreens
)

$ErrorActionPreference = 'Stop'

if (-not ('StillPoint.CaptureNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace StillPoint
{
    public static class CaptureNative
    {
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
        public static extern uint GetDpiForSystem();
    }
}
'@
}

# Make this process/thread physical-pixel aware BEFORE any GDI work.
# -4 = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2. Fails harmlessly if the
# hosting process was already made aware by someone else.
$processAware = [StillPoint.CaptureNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$threadAware = [StillPoint.CaptureNative]::SetThreadDpiAwarenessContext([IntPtr](-4))
$awareness = [StillPoint.CaptureNative]::GetAwarenessFromDpiAwarenessContext(
    [StillPoint.CaptureNative]::GetThreadDpiAwarenessContext())
# DPI_AWARENESS: 0 = unaware, 1 = system aware, 2 = per monitor aware
if ($awareness -eq 0) {
    throw "StillPoint capture.ps1: could not enable DPI awareness (process=$processAware thread=$threadAware). " +
          "Run in a fresh process; a DPI-unaware GDI capture would be virtualized (2048x1152 instead of 2560x1440)."
}

$dpi = [StillPoint.CaptureNative]::GetDpiForSystem()

if ($AllScreens) {
    $rectX = [StillPoint.CaptureNative]::GetSystemMetrics(76)  # SM_XVIRTUALSCREEN
    $rectY = [StillPoint.CaptureNative]::GetSystemMetrics(77)  # SM_YVIRTUALSCREEN
    $rectW = [StillPoint.CaptureNative]::GetSystemMetrics(78)  # SM_CXVIRTUALSCREEN
    $rectH = [StillPoint.CaptureNative]::GetSystemMetrics(79)  # SM_CYVIRTUALSCREEN
} else {
    $rectX = 0
    $rectY = 0
    $rectW = [StillPoint.CaptureNative]::GetSystemMetrics(0)   # SM_CXSCREEN
    $rectH = [StillPoint.CaptureNative]::GetSystemMetrics(1)   # SM_CYSCREEN
}

if ($rectW -le 0 -or $rectH -le 0) {
    throw "StillPoint capture.ps1: bad screen bounds ${rectW}x${rectH}"
}

$Out = [System.IO.Path]::GetFullPath($Out)
$outDir = [System.IO.Path]::GetDirectoryName($Out)
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

Add-Type -AssemblyName System.Drawing

$bitmap = New-Object System.Drawing.Bitmap -ArgumentList $rectW, $rectH
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $size = New-Object System.Drawing.Size -ArgumentList $rectW, $rectH
    $point = New-Object System.Drawing.Point -ArgumentList 0, 0
    $graphics.CopyFromScreen(
        (New-Object System.Drawing.Point -ArgumentList $rectX, $rectY),
        $point,
        $size,
        [System.Drawing.CopyPixelOperation]::SourceCopy)
} finally {
    $graphics.Dispose()
}

try {
    $bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $bitmap.Dispose()
}

$fileInfo = New-Object System.IO.FileInfo -ArgumentList $Out
Write-Output ("CAPTURED   {0}" -f $Out)
Write-Output ("IMAGE_SIZE {0}x{1} px" -f $rectW, $rectH)
Write-Output ("BOUNDS     x={0} y={1} (virtualdesktop={2})" -f $rectX, $rectY, [bool]$AllScreens)
Write-Output ("DPI        {0} dpi (scale {1}%), thread_awareness={2}, process_set={3}, thread_set={4}" -f `
    $dpi, [int](100.0 * $dpi / 96.0), $awareness, $processAware, $threadAware)
Write-Output ("FILE       {0} bytes" -f $fileInfo.Length)
