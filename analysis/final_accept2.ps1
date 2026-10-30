# Final acceptance on the shipped build: dots, dark theme, reset, exit.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class AcceptNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][AcceptNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$screenW = [AcceptNative]::GetSystemMetrics(0)
$screenH = [AcceptNative]::GetSystemMetrics(1)
$py = 'C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$measure = 'D:\dsh file\StillPoint\analysis\measure3.py'
$shots = 'D:\dsh file\StillPoint\analysis\shots'
$settings = Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json'

function Capture([string]$name) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($screenW, $screenH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $path = Join-Path $shots $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $path
}

function Mean-At([string]$png, [int]$x, [int]$y) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($png)
    $sum = 0.0; $n = 0
    for ($dy = -4; $dy -le 4; $dy += 2) {
        for ($dx = -4; $dx -le 4; $dx += 2) {
            $c = $bmp.GetPixel($x + $dx, $y + $dy)
            $sum += ($c.R + $c.G + $c.B) / 3.0; $n++
        }
    }
    $bmp.Dispose()
    return [math]::Round($sum / $n, 1)
}

function Find-Window([int]$procId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
    $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'StillPoint')
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.AndCondition($c1, $c2)))
}

function By-Id($win, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

function Select-Radio($win, [string]$id) {
    $el = By-Id $win $id
    if ($el) { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); return $true }
    return $false
}

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $settings -ErrorAction SilentlyContinue
Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:DOTNET_MULTILEVEL_LOOKUP -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700

$exe = 'D:\dsh file\StillPoint\dist\StillPoint\StillPoint.exe'
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$sw.Stop()
Write-Output ("launch: exited={0}  elapsed={1:N2}s" -f $proc.HasExited, $sw.Elapsed.TotalSeconds)

$png = Capture 'a1_shipped_light.png'
$out = & $py $measure $png 1.0 1.0 1.0 2560
Write-Output ("light dots: " + (($out | Select-String -Pattern '^SUMMARY').Line))

$win = Find-Window $proc.Id
if (-not $win) { throw 'settings window not found' }
$r = $win.Current.BoundingRectangle
$wx = [int]$r.X; $wy = [int]$r.Y; $ww = [int]$r.Width; $wh = [int]$r.Height
Write-Output ("window: {0},{1} {2}x{3}" -f $wx, $wy, $ww, $wh)
Write-Output ("light card pixel=" + (Mean-At $png ($wx + 120) ($wy + 200)))

Write-Output ("select dark: " + (Select-Radio $win 'ThemeDark'))
Start-Sleep -Milliseconds 1500
$png = Capture 'a2_shipped_dark.png'
Write-Output ("dark card pixel=" + (Mean-At $png ($wx + 120) ($wy + 200)))
$json = Get-Content $settings -Raw | ConvertFrom-Json
Write-Output ("settings DarkTheme=" + $json.DarkTheme)
Write-Output ("dark dots: " + ((& $py $measure $png 1.0 1.0 1.0 2560 | Select-String -Pattern '^SUMMARY').Line))

Write-Output ("select light: " + (Select-Radio $win 'ThemeLight'))
Start-Sleep -Milliseconds 1200
$png = Capture 'a3_shipped_light_again.png'
Write-Output ("light card pixel again=" + (Mean-At $png ($wx + 120) ($wy + 200)))

# reset everything through the footer button (built from char codes: 重置全部设置)
$resetName = [string]::Join('', [char[]](0x91CD,0x7F6E,0x5168,0x90E8,0x8BBE,0x7F6E))
$c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $resetName)
$reset = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
if ($reset) { $reset.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output 'reset invoked' } else { Write-Output 'RESET BUTTON NOT FOUND' }
Start-Sleep -Milliseconds 1500
$json = Get-Content $settings -Raw | ConvertFrom-Json
Write-Output ("after reset: Enabled=" + $json.Enabled + " DotColorHex=" + $json.DotColorHex + " DarkTheme=" + $json.DarkTheme + " Hotkey=" + $json.Hotkey)

# exit through the footer button (退出 StillPoint)
$exitName = [string]::Join('', [char[]](0x9000,0x51FA,0x20,0x53,0x74,0x69,0x6C,0x6C,0x50,0x6F,0x69,0x6E,0x74))
$c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $exitName)
$exit = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c2)
if ($exit) { $exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Write-Output 'exit invoked' } else { Write-Output 'EXIT BUTTON NOT FOUND' }
Start-Sleep -Seconds 2
$alive = [bool](Get-Process -Id $proc.Id -ErrorAction SilentlyContinue)
Write-Output ("process alive after exit: " + $alive)
$png = Capture 'a4_after_exit.png'
$out = & $py $measure $png 1.0 1.0 1.0 2560
Write-Output ("after exit dots: " + (($out | Select-String -Pattern '^SUMMARY').Line))

$root = [System.Windows.Automation.AutomationElement]::RootElement
$c3 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $c3)
Write-Output ("StillPoint top-level windows left: " + $wins.Count)
Write-Output 'DONE'
