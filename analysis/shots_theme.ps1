# Capture light + dark settings windows, top and bottom, from the Debug build.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Dpi2 {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][Dpi2]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:screenW = [Dpi2]::GetSystemMetrics(0)
$script:screenH = [Dpi2]::GetSystemMetrics(1)

function Capture([string]$path) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($script:screenW, $script:screenH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "CAPTURED $path"
}

function FindSettings([int]$procId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procId)
    $c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'StillPoint')
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.AndCondition($c1, $c2)))
}

function FindById($win, [string]$id) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

function SelectRadio($el) {
    $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
}

function ScrollBottom($win) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
    $sc = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($sc) {
        $sc.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)
        Start-Sleep -Milliseconds 700
        return $true
    }
    return $false
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$exe = 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'
$shots = 'D:\dsh file\StillPoint\analysis\shots'

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 4
$win = FindSettings $proc.Id
if (-not $win) { throw 'settings window not found' }

SelectRadio (FindById $win 'ThemeLight')
Start-Sleep -Milliseconds 700
Capture "$shots\v1_light_top.png"
[void](ScrollBottom $win)
Capture "$shots\v2_light_bottom.png"

SelectRadio (FindById $win 'ThemeDark')
Start-Sleep -Milliseconds 700
Capture "$shots\v3_dark_bottom.png"
[void](ScrollBottom $win)
Start-Sleep -Milliseconds 300
Capture "$shots\v4_dark_bottom2.png"

# back to the top of the dark window for the hero shot
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$sc = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
$sc.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern).SetScrollPercent(0, 0)
Start-Sleep -Milliseconds 700
Capture "$shots\v5_dark_top.png"

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
