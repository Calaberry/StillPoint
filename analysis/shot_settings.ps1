param(
    [string]$OutDir = "D:\dsh file\StillPoint\analysis\shots"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Shot {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[Shot]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$W = [Shot]::GetSystemMetrics(0)
$H = [Shot]::GetSystemMetrics(1)

function CaptureFull($path) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "captured $path"
}

$proc = Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $proc) { Write-Output "stillpoint not running"; exit 1 }
$rootEl = [System.Windows.Automation.AutomationElement]::RootElement
$pc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$nc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")
$and = New-Object System.Windows.Automation.AndCondition($pc, $nc)
$win = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $and)
if ($null -eq $win) { Write-Output "settings window not found"; exit 1 }

$r = New-Object Shot+RECT
[void][Shot]::GetWindowRect([IntPtr]$win.Current.NativeWindowHandle, [ref]$r)
$winW = $r.Right - $r.Left
$winH = $r.Bottom - $r.Top
Write-Output "window rect $($r.Left),$($r.Top) ${winW}x${winH}"

CaptureFull (Join-Path $OutDir "p8_settings_top.png")

# scroll the body to the bottom
$spCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Pane)
$spCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$panes = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $spCond)
Write-Output "scrollable elements: $($panes.Count)"
$scrolled = $false
if ($panes.Count -gt 0) {
    $sp = $panes[0].GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    try {
        $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)
        $scrolled = $true
    } catch {
        try {
            $sp.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount, [System.Windows.Automation.ScrollAmount]::LargeIncrement)
            $scrolled = $true
        } catch { Write-Output "scroll failed: $($_.Exception.Message)" }
    }
}
Write-Output "scrolled=$scrolled"
Start-Sleep -Milliseconds 600
CaptureFull (Join-Path $OutDir "p8_settings_bottom.png")

