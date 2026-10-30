# Final acceptance pass: defaults, hide settings, tray reopen, reset, exit.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Screen4 {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
}
'@
[Screen4]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$py = "C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
$shots = "D:\dsh file\StillPoint\analysis\shots"
$click = "D:\dsh file\StillPoint\tools\clicktest.ps1"
$measure = "D:\dsh file\StillPoint\analysis\measure2.py"
$settingsPath = "$env:LOCALAPPDATA\StillPoint\settings.json"

$exitCaption = [string]::Join('', [char[]](0x9000, 0x51FA, 0x20, 0x53, 0x74, 0x69, 0x6C, 0x6C, 0x50, 0x6F, 0x69, 0x6E, 0x74))
$resetCaption = [string]::Join('', [char[]](0x91CD, 0x7F6E, 0x5168, 0x90E8, 0x8BBE, 0x7F6E))
$closeCaption = [string]::Join('', [char[]](0x5173, 0x95ED))

function Capture([string]$path) {
    $w = [Screen4]::GetSystemMetrics(0); $h = [Screen4]::GetSystemMetrics(1)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose(); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

function Get-SettingsWindow([int]$processId) {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $processId)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")))
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
}

function Find-ButtonByName([System.Windows.Automation.AutomationElement]$win, [string]$name) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

# --- fresh start with defaults -------------------------------------------------
Stop-Process -Name StillPoint -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 700
Remove-Item $settingsPath -Force -ErrorAction SilentlyContinue
Remove-Item Env:\DOTNET_ROOT -ErrorAction SilentlyContinue

$exe = 'D:\dsh file\StillPoint\dist\StillPoint\StillPoint.exe'
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5
Write-Output ("[1] started pid=" + $proc.Id)

$win = Get-SettingsWindow $proc.Id
if (-not $win) { throw "settings window not visible at startup" }
Write-Output "[1] settings window visible at startup = True"

# --- close the settings window: overlay must keep running ---------------------
$close = Find-ButtonByName $win $closeCaption
if (-not $close) { throw "close button not found" }
$close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 1200
$win = Get-SettingsWindow $proc.Id
Write-Output ("[2] settings window after close = " + ($null -ne $win))
Start-Sleep -Milliseconds 500
Capture (Join-Path $shots "a1_settings_closed.png")
& $py $measure (Join-Path $shots "a1_settings_closed.png") | Select-String -Pattern "dots with peak"

# --- reopen from the tray overflow -------------------------------------------
# Open the Windows 11 tray overflow chevron, then double click the StillPoint icon.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $click -X 2200 -Y 1400 -Clicks 1 | Out-Null
Start-Sleep -Milliseconds 1200
Capture (Join-Path $shots "a2_overflow.png")
Write-Output "[3] overflow captured"

# The icon grid is at a fixed spot in the overflow flyout; double click it.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $click -X 2100 -Y 1230 -Clicks 2 | Out-Null
Start-Sleep -Milliseconds 1500
$win = Get-SettingsWindow $proc.Id
Write-Output ("[3] settings window after tray double click = " + ($null -ne $win))

if (-not $win) {
    # Fall back: reopen via the tray menu is not scriptable here; report and continue.
    Write-Output "[3] tray reopen NOT observed - needs manual check"
} else {
    $reset = Find-ButtonByName $win $resetCaption
    if ($reset) {
        $reset.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 900
        Write-Output "[4] reset-all invoked"
        if (Test-Path $settingsPath) {
            $json = Get-Content $settingsPath -Raw
            Write-Output ("[4] settings.json length=" + $json.Length)
        }
    } else {
        Write-Output "[4] reset button not found"
    }

    $exit = Find-ButtonByName $win $exitCaption
    if ($exit) {
        $exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Seconds 3
        $still = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
        Write-Output ("[5] process alive after quit = " + ($null -ne $still))
    } else {
        Write-Output "[5] exit button not found"
    }
}
Write-Output "done"
