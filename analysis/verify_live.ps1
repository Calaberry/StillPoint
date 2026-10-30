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
public class Ver {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
}
"@
[Ver]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$W = [Ver]::GetSystemMetrics(0)
$H = [Ver]::GetSystemMetrics(1)

function Capture($path) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "captured $(Split-Path $path -Leaf)"
}

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$proc = Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $proc) { Write-Output "FAIL: app not running"; exit 1 }

$rootEl = $AE::RootElement
$pc = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
$nc = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, "StillPoint")
$and = New-Object System.Windows.Automation.AndCondition($pc, $nc)
$win = $rootEl.FindFirst($TS::Children, $and)
if ($null -eq $win) { Write-Output "FAIL: settings window not found"; exit 1 }

function FindById($id) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)
    return $win.FindFirst($TS::Descendants, $c)
}

function SetSlider($id, [double]$value) {
    $el = FindById $id
    if ($null -eq $el) { Write-Output "FAIL: slider $id missing"; return }
    $p = $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $p.SetValue($value)
    Start-Sleep -Milliseconds 350
    Write-Output ("slider " + $id + " -> " + $p.Current.Value)
}

$shots = Join-Path $Root "analysis\shots"

# --- 1. baseline
Capture (Join-Path $shots "v1_baseline.png")

# --- 2. colour preset 薄荷 (mint)
$mint = "mint"
$mintName = ([string][char]0x8584 + [string][char]0x8377)   # 薄荷
$cc = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $mintName)
$sw = $win.FindFirst($TS::Descendants, $cc)
if ($null -ne $sw) {
    $tp = $sw.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $tp.Select()
    Start-Sleep -Milliseconds 400
    Write-Output "colour preset selected: $mintName"
} else {
    Write-Output "FAIL: mint swatch not found"
}
Capture (Join-Path $shots "v2_mint.png")

# --- 3. layout scale 80%
SetSlider "ScaleSlider" 0.8
Capture (Join-Path $shots "v3_scale80.png")
SetSlider "ScaleSlider" 1.0
Start-Sleep -Milliseconds 300

# --- 4. dot size 160%
SetSlider "DotSizeSlider" 1.6
Capture (Join-Path $shots "v4_size160.png")
SetSlider "DotSizeSlider" 1.0
Start-Sleep -Milliseconds 300

# --- 5. hide overlay
$toggle = FindById "OverlayToggleButton"
if ($null -ne $toggle) {
    $toggle.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
    Write-Output ("overlay toggle -> " + $toggle.Current.Name)
}
Capture (Join-Path $shots "v5_hidden.png")
if ($null -ne $toggle) {
    $toggle.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
    Write-Output ("overlay toggle -> " + $toggle.Current.Name)
}
Capture (Join-Path $shots "v6_shown.png")

Write-Output "DONE"
