# Documentation screenshots with a non-blank guard (WPF can blank for a frame right after a theme swap).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DocShotNative2 {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][DocShotNative2]::SetProcessDpiAwarenessContext([IntPtr](-4))
$screenW = [DocShotNative2]::GetSystemMetrics(0)
$screenH = [DocShotNative2]::GetSystemMetrics(1)
$shots = 'D:\dsh file\StillPoint\analysis\shots'

function Snap([string]$name) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($screenW, $screenH)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $path = Join-Path $shots $name
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $path
}

function Region-StdDev([string]$png, [int]$x, [int]$y, [int]$w, [int]$h) {
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap($png)
    $sum = 0.0; $sum2 = 0.0; $n = 0
    for ($py = $y; $py -lt ($y + $h); $py += 9) {
        for ($px = $x; $px -lt ($x + $w); $px += 9) {
            $c = $bmp.GetPixel($px, $py)
            $v = ($c.R + $c.G + $c.B) / 3.0
            $sum += $v; $sum2 += $v * $v; $n++
        }
    }
    $bmp.Dispose()
    $mean = $sum / $n
    return [math]::Sqrt([math]::Max(0.0, ($sum2 / $n) - ($mean * $mean)))
}

function Capture-Checked([string]$name, [int]$x, [int]$y, [int]$w, [int]$h) {
    for ($attempt = 1; $attempt -le 6; $attempt++) {
        Start-Sleep -Milliseconds 700
        $png = Snap $name
        $sd = Region-StdDev $png $x $y $w $h
        if ($sd -gt 6.0) {
            Write-Output ("CAPTURED {0} (stddev {1:N1}, attempt {2})" -f $png, $sd, $attempt)
            return $png
        }
        Write-Output ("  blank-ish (stddev {0:N1}), retrying" -f $sd)
    }
    Write-Output "CAPTURE FAILED (still blank): $name"
    return $null
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

function Scroll-To($win, [int]$percent) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
    $sc = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $sc) { return $false }
    $pattern = $sc.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        try { $pattern.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $percent); Start-Sleep -Milliseconds 500; return $true }
        catch { Start-Sleep -Milliseconds 500 }
    }
    return $false
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item (Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json') -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
$proc = Start-Process -FilePath 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe' -PassThru
Start-Sleep -Seconds 5
$win = Find-Window $proc.Id
if (-not $win) { throw 'settings window not found' }
$r = $win.Current.BoundingRectangle
$wx = [int]$r.X; $wy = [int]$r.Y; $ww = [int]$r.Width; $wh = [int]$r.Height
Write-Output "window rect $wx,$wy ${ww}x${wh}"

[void](Scroll-To $win 0)
Write-Output (Select-Radio $win 'ThemeLight')
Capture-Checked 'w1_light_top.png' $wx $wy $ww $wh | Out-Null

Write-Output (Select-Radio $win 'ThemeDark')
Capture-Checked 'w2_dark_top.png' $wx $wy $ww $wh | Out-Null

[void](Scroll-To $win 100)
Capture-Checked 'w3_dark_bottom.png' $wx $wy $ww $wh | Out-Null

Write-Output (Select-Radio $win 'ThemeLight')
[void](Scroll-To $win 100)
Capture-Checked 'w4_light_bottom.png' $wx $wy $ww $wh | Out-Null

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
