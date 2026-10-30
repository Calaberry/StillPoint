# Take the final documentation screenshots from the shipped build.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Screen3 {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
}
'@
[Screen3]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$shots = "D:\dsh file\StillPoint\analysis\shots"
$customCaption = [string]::Join('', [char[]](0x81EA, 0x9009, 0x2026))
$doneCaption = [string]::Join('', [char[]](0x5B8C, 0x6210))

function Capture([string]$path) {
    $w = [Screen3]::GetSystemMetrics(0); $h = [Screen3]::GetSystemMetrics(1)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Output ("captured " + $path)
}

Stop-Process -Name StillPoint -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
Remove-Item "$env:LOCALAPPDATA\StillPoint\settings.json" -Force -ErrorAction SilentlyContinue
Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue

Start-Process -FilePath 'D:\dsh file\StillPoint\dist\StillPoint\StillPoint.exe' | Out-Null
Start-Sleep -Seconds 5
$proc = Get-Process -Name StillPoint
$procId = $proc.Id

$root = [System.Windows.Automation.AutomationElement]::RootElement
$winCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)),
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")))
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $winCond)
if (-not $win) { throw "settings window not found" }

# Bring the settings window to the front without moving it.
$win.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState(
    [System.Windows.Automation.WindowVisualState]::Normal)
Start-Sleep -Milliseconds 700
Capture (Join-Path $shots "z1_settings_top.png")

# Open the custom colour picker and capture it.
$c = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)
$label = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$btn = $label
while ($btn -and $btn.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { $btn = $walker.GetParent($btn) }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900
Capture (Join-Path $shots "z2_colour_picker.png")

$dc = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $doneCaption)
$done = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $dc)
if ($done) { $done.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
Start-Sleep -Milliseconds 600

# Scroll the body to the bottom and capture the runtime card.
$scond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$scrollers = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $scond)
Write-Output ("scrollable elements: " + $scrollers.Count)
if ($scrollers.Count -gt 0) {
    $sp = $scrollers[0].GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)
    Write-Output "scrolled"
}
Start-Sleep -Milliseconds 800
Capture (Join-Path $shots "z3_settings_runtime.png")

Write-Output "ok"
