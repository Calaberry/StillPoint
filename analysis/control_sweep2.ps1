# Control sweep (v2): prove every appearance/layout control changes the real overlay.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SweepNative2 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
'@
[void][SweepNative2]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:screenW = [SweepNative2]::GetSystemMetrics(0)
$script:screenH = [SweepNative2]::GetSystemMetrics(1)

$py = 'C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$measure = 'D:\dsh file\StillPoint\analysis\measure3.py'
$shots = 'D:\dsh file\StillPoint\analysis\shots\sweep'
New-Item -ItemType Directory -Force -Path $shots | Out-Null
$script:maxX = 1500   # settings window is parked to the right of this

function Capture([string]$name) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($script:screenW, $script:screenH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $path = Join-Path $shots $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $path
}

function Measure-Dots([string]$png, [double]$scale = 1.0, [double]$sh = 1.0, [double]$sv = 1.0) {
    $out = & $py $measure $png $scale $sh $sv $script:maxX
    $summary = ($out | Select-String -Pattern '^SUMMARY').Line
    $probes = ($out | Where-Object { $_ -match '^\s+\(0,0\)|^\s+\(0,3\)' }) -join ' ; '
    return "$summary`n    probes: $($probes.Trim())"
}

function Find-Window([int]$procId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
    $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'StillPoint')
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.AndCondition($c1, $c2)))
}

function By-Id($win, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    $el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
    if (-not $el) { throw "control not found: $id" }
    return $el
}

function Set-Slider($win, [string]$id, [double]$value) {
    (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($value)
}

function Invoke-El($win, [string]$id) {
    (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Select-Radio($win, [string]$id) {
    (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

function Toggle-El($win, [string]$id, [bool]$on) {
    $pattern = (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $on) { $pattern.Toggle() }
}

function Hotkey-Press {
    [SweepNative2]::keybd_event(0x11, 0, 0, [IntPtr]::Zero)
    [SweepNative2]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)
    [SweepNative2]::keybd_event(0x44, 0, 0, [IntPtr]::Zero)
    [SweepNative2]::keybd_event(0x44, 0, 2, [IntPtr]::Zero)
    [SweepNative2]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
    [SweepNative2]::keybd_event(0x11, 0, 2, [IntPtr]::Zero)
}

function Read-Settings {
    Start-Sleep -Milliseconds 1300
    return (Get-Content (Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json') -Raw | ConvertFrom-Json)
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$exe = 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item (Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json') -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$win = Find-Window $proc.Id
if (-not $win) { throw 'settings window not found' }

$rect = New-Object SweepNative2+RECT
$hwnd = [IntPtr]$win.Current.NativeWindowHandle
[void][SweepNative2]::GetWindowRect($hwnd, [ref]$rect)
$winW = $rect.Right - $rect.Left
$winH = $rect.Bottom - $rect.Top
[void][SweepNative2]::SetWindowPos($hwnd, [IntPtr]::Zero, ($script:screenW - $winW - 20), 120, $winW, $winH, 0x0014)
Start-Sleep -Milliseconds 900
Select-Radio $win 'ThemeLight'
Start-Sleep -Milliseconds 400

Write-Output '== 1 baseline (glow 13%) =='
Write-Output (Measure-Dots (Capture 'q0_baseline.png'))

Write-Output '== 2 glow 0% =='
Set-Slider $win 'GlowSlider' 0.0
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q1_glow0.png'))

Write-Output '== 3 glow 45% =='
Set-Slider $win 'GlowSlider' 0.45
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q2_glow45.png'))

Write-Output '== 4 opacity 30% =='
Set-Slider $win 'OpacitySlider' 0.30
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q3_opacity30.png'))

Write-Output '== 5 dot size 180% (opacity back to 94%) =='
Set-Slider $win 'OpacitySlider' 0.94
Set-Slider $win 'DotSizeSlider' 1.8
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q4_dotsize180.png'))

Write-Output '== 6 scale 85% (dot size 100%) =='
Set-Slider $win 'DotSizeSlider' 1.0
Set-Slider $win 'ScaleSlider' 0.85
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q5_scale85.png') 0.85)

Write-Output '== 7 spacing 112% linked (scale 100%) =='
Set-Slider $win 'ScaleSlider' 1.0
Set-Slider $win 'SpacingSlider' 1.12
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q6_spacing112.png') 1.0 1.12 1.12)
$json = Read-Settings
Write-Output ("    settings LinkSpacing=" + $json.LinkSpacing + " H=" + $json.SpacingHorizontal + " V=" + $json.SpacingVertical)

Write-Output '== 8 separate spacing H 112% / V 100% =='
Toggle-El $win 'SeparateSpacingToggle' $true
Start-Sleep -Milliseconds 500
Set-Slider $win 'SpacingHSlider' 1.12
Set-Slider $win 'SpacingVSlider' 1.0
Start-Sleep -Milliseconds 600
Write-Output (Measure-Dots (Capture 'q7_separate.png') 1.0 1.12 1.0)
$json = Read-Settings
Write-Output ("    settings LinkSpacing=" + $json.LinkSpacing + " H=" + $json.SpacingHorizontal + " V=" + $json.SpacingVertical)

Write-Output '== 9 reset layout =='
Toggle-El $win 'SeparateSpacingToggle' $false
Start-Sleep -Milliseconds 400
Invoke-El $win 'ResetLayoutButton'
Start-Sleep -Milliseconds 800
Write-Output (Measure-Dots (Capture 'q8_reset_layout.png'))
$json = Read-Settings
Write-Output ("    settings Scale=" + $json.LayoutScale + " H=" + $json.SpacingHorizontal + " V=" + $json.SpacingVertical + " Link=" + $json.LinkSpacing)

Write-Output '== 10 preview backdrop segments =='
foreach ($id in @('BackdropLight', 'BackdropDark', 'BackdropGame')) {
    Select-Radio $win $id
    $json = Read-Settings
    Write-Output ("    " + $id + " -> PreviewBackdrop=" + $json.PreviewBackdrop)
}

Write-Output '== 11 overlay hide / show =='
Invoke-El $win 'OverlayToggleButton'
Start-Sleep -Milliseconds 1000
Write-Output (Measure-Dots (Capture 'q9_hidden.png'))
Invoke-El $win 'OverlayToggleButton'
Start-Sleep -Milliseconds 1000
Write-Output (Measure-Dots (Capture 'q10_shown.png'))

Write-Output '== 12 hotkey toggle disabled -> press does nothing =='
Toggle-El $win 'HotkeyToggle' $false
Start-Sleep -Milliseconds 500
Hotkey-Press
Start-Sleep -Milliseconds 1000
Write-Output (Measure-Dots (Capture 'q11_hotkey_disabled_press.png'))

Write-Output '== 13 hotkey toggle enabled -> press hides dots =='
Toggle-El $win 'HotkeyToggle' $true
Start-Sleep -Milliseconds 700
Hotkey-Press
Start-Sleep -Milliseconds 1000
Write-Output (Measure-Dots (Capture 'q12_hotkey_enabled_press.png'))
Hotkey-Press
Start-Sleep -Milliseconds 800
Write-Output (Measure-Dots (Capture 'q13_hotkey_restored.png'))

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
