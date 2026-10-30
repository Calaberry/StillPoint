# Exercise the custom colour picker: open popup, click in the hue/SV area, measure the overlay dot colour.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DpiShot {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[DpiShot]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$shots = "D:\dsh file\StillPoint\analysis\shots"
$py = "C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"

function Capture([string]$path) {
    $w = [DpiShot]::GetSystemMetrics(0); $h = [DpiShot]::GetSystemMetrics(1)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

function Find-PopupWindow([int]$processId) {
    $found = [IntPtr]::Zero
    $cb = [DpiShot+EnumProc]{
        param($h, $p)
        $sb = New-Object System.Text.StringBuilder 64
        [DpiShot]::GetClassName($h, $sb, 64) | Out-Null
        if ($sb.ToString() -eq 'Popup') {
            $procId = 0
            [DpiShot]::GetWindowThreadProcessId($h, [ref]$procId) | Out-Null
            if ($procId -eq $processId) { $script:popupHwnd = $h; return $false }
        }
        return $true
    }
    [DpiShot]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:popupHwnd
}

$proc = Get-Process -Name StillPoint -ErrorAction SilentlyContinue
if (-not $proc) {
    $env:DOTNET_ROOT = "D:\dsh file\StillPoint\.dotnet"
    Start-Process -FilePath "D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe" | Out-Null
    Start-Sleep -Seconds 5
    $proc = Get-Process -Name StillPoint
}
$procId = $proc.Id
Write-Output ("stillpoint pid=" + $procId)

$root = [System.Windows.Automation.AutomationElement]::RootElement
# U+81EA U+9009 U+2026 = "custom colour" button caption; keep the source ASCII-only.
$customCaption = [string]::Join('', [char[]](0x81EA, 0x9009, 0x2026))
$doneCaption = [string]::Join('', [char[]](0x5B8C, 0x6210))
$cond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)),
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")))
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if (-not $win) { throw "settings window not found" }

$btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)
$label = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $label) { throw "custom colour label not found" }
$btn = $label
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
while ($btn -and $btn.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
    $btn = $walker.GetParent($btn)
}
if (-not $btn) { throw "custom colour button not found" }
$inv = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
$inv.Invoke()
Start-Sleep -Milliseconds 900

$script:popupHwnd = [IntPtr]::Zero
$hwnd = Find-PopupWindow $procId
Write-Output ("popup hwnd=" + $hwnd)
$r = New-Object DpiShot+RECT
[DpiShot]::GetWindowRect($hwnd, [ref]$r) | Out-Null
Write-Output ("popup rect=" + $r.Left + "," + $r.Top + "," + $r.Right + "," + $r.Bottom)

Capture (Join-Path $shots "k1_popup.png")

# Click inside the saturation/value plane (right-hand side = saturated, upper = bright).
$px = [int]($r.Left + 150)
$clickY = [int]($r.Top + 60)
Start-Sleep -Milliseconds 200
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\dsh file\StillPoint\tools\clicktest.ps1" -X $px -Y $clickY -Clicks 1 | Select-String -Pattern "TARGET|CLICK" | Out-Null
Start-Sleep -Milliseconds 800
Capture (Join-Path $shots "k2_after_pick.png")
& $py "D:\dsh file\StillPoint\analysis\measure2.py" (Join-Path $shots "k2_after_pick.png") | Select-String -Pattern "peak colour|dots with peak"

# Click ??? to dismiss the popup.
$doneCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $doneCaption)
$popupEl = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
if ($popupEl) {
    $done = $popupEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $doneCond)
    if ($done) {
        $done.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Write-Output "done clicked"
    }
}
Start-Sleep -Milliseconds 600
Capture (Join-Path $shots "k3_popup_closed.png")
Write-Output "ok"

