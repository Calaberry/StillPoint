# Control sweep: prove every appearance/layout control actually changes the overlay.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SweepNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
}
'@
[void][SweepNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:screenW = [SweepNative]::GetSystemMetrics(0)
$script:screenH = [SweepNative]::GetSystemMetrics(1)

$py = 'C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$measure = 'D:\dsh file\StillPoint\analysis\measure3.py'
$shots = 'D:\dsh file\StillPoint\analysis\shots\sweep'
New-Item -ItemType Directory -Force -Path $shots | Out-Null

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
    $out = & $py $measure $png $scale $sh $sv
    $summary = ($out | Select-String -Pattern '^SUMMARY').Line
    $probe = ($out | Where-Object { $_ -match '^\s+\(0,0\)' })
    return "$summary | $($probe.Trim())"
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
    $el = By-Id $win $id
    $el.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue($value)
}

function Invoke-El($win, [string]$id) {
    (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Select-Radio($win, [string]$id) {
    (By-Id $win $id).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

function Toggle-El($win, [string]$id, [bool]$on) {
    $el = By-Id $win $id
    $pattern = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if (($pattern.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) -ne $on) { $pattern.Toggle() }
}

function Hotkey-Press {
    # Ctrl+Alt+D
    [SweepNative]::keybd_event(0x11, 0, 0, [IntPtr]::Zero)
    [SweepNative]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)
    [SweepNative]::keybd_event(0x44, 0, 0, [IntPtr]::Zero)
    [SweepNative]::keybd_event(0x44, 0, 2, [IntPtr]::Zero)
    [SweepNative]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
    [SweepNative]::keybd_event(0x11, 0, 2, [IntPtr]::Zero)
}

function Read-Settings {
    $p = Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json'
    return (Get-Content $p -Raw | ConvertFrom-Json)
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

# Park the settings window bottom-right so the top-left dots stay measurable.
$rect = New-Object SweepNative+RECT
$hwnd = [IntPtr]$win.Current.NativeWindowHandle
[void][SweepNative]::GetWindowRect($hwnd, [ref]$rect)
$winW = $rect.Right - $rect.Left
$winH = $rect.Bottom - $rect.Top
$newX = $script:screenW - $winW - 20
$newY = 120
[void][SweepNative]::SetWindowPos($hwnd, [IntPtr]::Zero, $newX, $newY, $winW, $winH, 0x0014)
Start-Sleep -Milliseconds 800

Select-Radio $win 'ThemeLight'
Start-Sleep -Milliseconds 300

Write-Output '== baseline =='
$png = Capture 's0_baseline.png'
Write-Output (Measure-Dots $png)

Write-Output '== glow 0.00 =='
Set-Slider $win 'GlowSlider' 0.0
Start-Sleep -Milliseconds 500
$png = Capture 's1_glow0.png'
Write-Output (Measure-Dots $png)

Write-Output '== glow 0.45 =='
Set-Slider $win 'GlowSlider' 0.45
Start-Sleep -Milliseconds 500
$png = Capture 's2_glow45.png'
Write-Output (Measure-Dots $png)

Write-Output '== opacity 0.30 =='
Set-Slider $win 'OpacitySlider' 0.30
Start-Sleep -Milliseconds 500
$png = Capture 's3_opacity30.png'
Write-Output (Measure-Dots $png)

Write-Output '== opacity 0.94, dot size 1.8 =='
Set-Slider $win 'OpacitySlider' 0.94
Set-Slider $win 'DotSizeSlider' 1.8
Start-Sleep -Milliseconds 500
$png = Capture 's4_dotsize18.png'
Write-Output (Measure-Dots $png)

Write-Output '== dot size 1.0, scale 1.20 =='
Set-Slider $win 'DotSizeSlider' 1.0
Set-Slider $win 'ScaleSlider' 1.2
Start-Sleep -Milliseconds 500
$png = Capture 's5_scale120.png'
Write-Output (Measure-Dots $png 1.2)

Write-Output '== scale 1.0, spacing 1.12 =='
Set-Slider $win 'ScaleSlider' 1.0
Set-Slider $win 'SpacingSlider' 1.12
Start-Sleep -Milliseconds 500
$png = Capture 's6_spacing112.png'
Write-Output (Measure-Dots $png 1.0 1.12)

Write-Output '== separate spacing: H 1.12 / V 1.0 =='
Toggle-El $win 'SeparateSpacingToggle' $true
Start-Sleep -Milliseconds 400
Set-Slider $win 'SpacingHSlider' 1.12
Set-Slider $win 'SpacingVSlider' 1.0
Start-Sleep -Milliseconds 500
$png = Capture 's7_separate.png'
Write-Output (Measure-Dots $png 1.0 1.12 1.0)
$json = Read-Settings
Write-Output ("settings: LinkSpacing=" + $json.LinkSpacing + " SpacingH=" + $json.SpacingHorizontal + " SpacingV=" + $json.SpacingVertical)

Write-Output '== reset layout =='
Toggle-El $win 'SeparateSpacingToggle' $false
Start-Sleep -Milliseconds 300
Invoke-El $win 'ResetLayoutButton'
Start-Sleep -Milliseconds 600
$png = Capture 's8_reset_layout.png'
Write-Output (Measure-Dots $png)
$json = Read-Settings
Write-Output ("settings: Scale=" + $json.LayoutScale + " SpacingH=" + $json.SpacingHorizontal + " SpacingV=" + $json.SpacingVertical + " LinkSpacing=" + $json.LinkSpacing)

Write-Output '== preview backdrop segments =='
foreach ($id in @('BackdropLight', 'BackdropDark', 'BackdropGame')) {
    Select-Radio $win $id
    Start-Sleep -Milliseconds 400
    $json = Read-Settings
    Write-Output ("backdrop " + $id + " -> settings.PreviewBackdrop=" + $json.PreviewBackdrop)
}

Write-Output '== overlay hide / show =='
Invoke-El $win 'OverlayToggleButton'
Start-Sleep -Milliseconds 900
$png = Capture 's9_hidden.png'
Write-Output (Measure-Dots $png)
Invoke-El $win 'OverlayToggleButton'
Start-Sleep -Milliseconds 900
$png = Capture 's10_shown.png'
Write-Output (Measure-Dots $png)

Write-Output '== hotkey disabled =='
Toggle-El $win 'HotkeyToggle' $false
Start-Sleep -Milliseconds 400
Hotkey-Press
Start-Sleep -Milliseconds 900
$png = Capture 's11_hotkey_off_press.png'
Write-Output (Measure-Dots $png)
Write-Output '== hotkey enabled =='
Toggle-El $win 'HotkeyToggle' $true
Start-Sleep -Milliseconds 600
Hotkey-Press
Start-Sleep -Milliseconds 900
$png = Capture 's12_hotkey_on_press.png'
Write-Output (Measure-Dots $png)
Hotkey-Press
Start-Sleep -Milliseconds 900

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
