# Documentation screenshots from the current build: light top, dark top, dark bottom.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DocShotNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][DocShotNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$script:screenW = [DocShotNative]::GetSystemMetrics(0)
$script:screenH = [DocShotNative]::GetSystemMetrics(1)
$shots = 'D:\dsh file\StillPoint\analysis\shots'

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
    if ($el) { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
}

function Scroll-To($win, [int]$percent) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
    $sc = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $sc) { return $false }
    $pattern = $sc.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        try {
            $pattern.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $percent)
            Start-Sleep -Milliseconds 600
            return $true
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item (Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json') -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
$proc = Start-Process -FilePath 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe' -PassThru
Start-Sleep -Seconds 4
$win = Find-Window $proc.Id
if (-not $win) { throw 'settings window not found' }

Select-Radio $win 'ThemeLight'
[void](Scroll-To $win 0)
Start-Sleep -Milliseconds 500
Write-Output (Capture 'z1_light_top.png')

Select-Radio $win 'ThemeDark'
Start-Sleep -Milliseconds 700
[void](Scroll-To $win 0)
Start-Sleep -Milliseconds 400
Write-Output (Capture 'z2_dark_top.png')

[void](Scroll-To $win 100)
Start-Sleep -Milliseconds 500
Write-Output (Capture 'z3_dark_bottom.png')

Select-Radio $win 'ThemeLight'
[void](Scroll-To $win 100)
Start-Sleep -Milliseconds 600
Write-Output (Capture 'z4_light_bottom.png')

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
