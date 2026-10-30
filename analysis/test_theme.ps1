# Dark theme end-to-end test: light -> dark -> restart persists -> back to light.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Dpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@

$prev = [Dpi]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:screenW = [Dpi]::GetSystemMetrics(0)
$script:screenH = [Dpi]::GetSystemMetrics(1)

function Capture([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($script:screenW, $script:screenH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "CAPTURED $path"
}

function FindWindow([string]$name, [int]$procId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
    $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $cond = New-Object System.Windows.Automation.AndCondition($c1, $c2)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

function FindById($win, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

function SelectRadio($el) {
    $p = $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $p.Select()
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$exe = 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'
$shots = 'D:\dsh file\StillPoint\analysis\shots'
$settingsPath = Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json'

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$w = FindWindow 'StillPoint' $p.Id
if (-not $w) { throw 'settings window not found' }
Capture "$shots\t1_light.png"

SelectRadio (FindById $w 'ThemeDark')
Start-Sleep -Milliseconds 900
Capture "$shots\t2_dark.png"
Write-Output ('settings.json DarkTheme=' + (Get-Content $settingsPath -Raw | ConvertFrom-Json).DarkTheme)

# restart: the dark choice must survive
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 700
$p2 = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$w2 = FindWindow 'StillPoint' $p2.Id
if (-not $w2) { throw 'settings window not found after restart' }
Capture "$shots\t3_dark_restart.png"

SelectRadio (FindById $w2 'ThemeLight')
Start-Sleep -Milliseconds 900
Capture "$shots\t4_back_light.png"
Write-Output ('settings.json DarkTheme=' + (Get-Content $settingsPath -Raw | ConvertFrom-Json).DarkTheme)

Get-Process -Id $p2.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
