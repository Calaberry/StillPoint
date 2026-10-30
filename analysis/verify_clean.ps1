param(
    [string]$Root = "D:\dsh file\StillPoint"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class V2 {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
}
"@
[V2]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$W = [V2]::GetSystemMetrics(0)
$H = [V2]::GetSystemMetrics(1)

function Capture($path) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("captured " + (Split-Path $path -Leaf))
}

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$proc = Get-Process -Name StillPoint | Select-Object -First 1
$rootEl = $AE::RootElement
$pc = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
$nc = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, "StillPoint")
$win = $rootEl.FindFirst($TS::Children, (New-Object System.Windows.Automation.AndCondition($pc, $nc)))
if ($null -eq $win) { Write-Output "FAIL: settings window missing"; exit 1 }

# set 0.8 scale
$c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "ScaleSlider")
$sl = $win.FindFirst($TS::Descendants, $c)
$sl.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(0.8)
Start-Sleep -Milliseconds 400
Write-Output "scale set to 0.8"

# hide the settings window (button AutomationId CloseButton) so nothing covers the overlay
$cl = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, "CloseButton")
$btn = $win.FindFirst($TS::Descendants, $cl)
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 800
Capture (Join-Path $Root "analysis\shots\v7_scale80_clean.png")

# hotkey toggles the overlay (Ctrl+Alt+D)
[V2]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)   # Ctrl down
[V2]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)   # Alt down
[V2]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)   # D down
[V2]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)
[V2]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
[V2]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Capture (Join-Path $Root "analysis\shots\v8_hidden_clean.png")

[V2]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
[V2]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
[V2]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)
[V2]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)
[V2]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
[V2]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 900
Capture (Join-Path $Root "analysis\shots\v9_shown_clean.png")
Write-Output "DONE"
