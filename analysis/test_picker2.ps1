# Drive the custom colour picker with UIA-derived geometry (ASCII source on purpose:
# Windows PowerShell 5.1 mis-reads UTF-8 literals, so captions are built from char codes).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Screen2 {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
}
'@
[Screen2]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$shots = "D:\dsh file\StillPoint\analysis\shots"
$py = "C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$click = "D:\dsh file\StillPoint\tools\clicktest.ps1"

$customCaption = [string]::Join('', [char[]](0x81EA, 0x9009, 0x2026))
$doneCaption = [string]::Join('', [char[]](0x5B8C, 0x6210))

function Capture([string]$path) {
    $w = [Screen2]::GetSystemMetrics(0); $h = [Screen2]::GetSystemMetrics(1)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

$proc = Get-Process -Name StillPoint -ErrorAction SilentlyContinue
if (-not $proc) { throw "StillPoint is not running" }
$procId = $proc.Id
$root = [System.Windows.Automation.AutomationElement]::RootElement
$winCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)),
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")))
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $winCond)
if (-not $win) { throw "settings window not found" }

function Find-ByName([System.Windows.Automation.AutomationElement]$scope, [string]$name) {
    $c = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $scope.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

# Open the popup.
$label = Find-ByName $win $customCaption
if (-not $label) { throw "custom colour label not found" }
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
$btn = $label
while ($btn -and $btn.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { $btn = $walker.GetParent($btn) }
if (-not $btn) { throw "custom colour button not found" }
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 900

# The popup is a separate top-level window: find the Done button anywhere under the desktop.
$done = Find-ByName $root $doneCaption
if (-not $done) { throw "done button not found (popup did not open)" }
$b = $done.Current.BoundingRectangle
Write-Output ("done button rect=" + [int]$b.Left + "," + [int]$b.Top + "," + [int]$b.Right + "," + [int]$b.Bottom)

# Picker geometry: 248x238 with 14 px padding, plane 140 tall, hue 14, value row 30.
$cardRight = [int]$b.Right + 14
$cardBottom = [int]$b.Bottom + 14
$cardLeft = $cardRight - 248
$cardTop = $cardBottom - 238
$planeLeft = $cardLeft + 14
$planeTop = $cardTop + 14
$clickX = [int]($planeLeft + 0.78 * 220)
$clickY = [int]($planeTop + 0.22 * 140)
Write-Output ("card=" + $cardLeft + "," + $cardTop + "  plane click=" + $clickX + "," + $clickY)

Capture (Join-Path $shots "m1_popup_open.png")
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $click -X $clickX -Y $clickY -Clicks 1 | Select-String -Pattern "CLICK"
Start-Sleep -Milliseconds 700
Capture (Join-Path $shots "m2_picked.png")
& $py "D:\dsh file\StillPoint\analysis\measure2.py" (Join-Path $shots "m2_picked.png") | Select-String -Pattern "peak colour|dots with peak"

# Hue strip: drag the ring to a different hue by clicking at 12 % of its width.
$hueX = [int]($planeLeft + 0.12 * 220)
$hueY = [int]($planeTop + 140 + 12 + 7)
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $click -X $hueX -Y $hueY -Clicks 1 | Select-String -Pattern "CLICK"
Start-Sleep -Milliseconds 700
Capture (Join-Path $shots "m3_hue.png")

# Finish.
$done.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 600
Capture (Join-Path $shots "m4_closed.png")
& $py "D:\dsh file\StillPoint\analysis\measure2.py" (Join-Path $shots "m4_closed.png") | Select-String -Pattern "peak colour|dots with peak"
Write-Output "ok"
