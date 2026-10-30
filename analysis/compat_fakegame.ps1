param(
    [string]$Mode = "fullscreen"
)

$ErrorActionPreference = "Stop"
$root = "D:\dsh file\StillPoint"
$env:DOTNET_ROOT = "$root\.dotnet"
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Compat {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
}
"@
[Compat]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$W = [Compat]::GetSystemMetrics(0)
$H = [Compat]::GetSystemMetrics(1)

function Capture($path) {
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "captured -> $path"
}

function Hide-SettingsWindow {
    $proc = Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $proc) { Write-Output "stillpoint not running"; return }
    $rootEl = [System.Windows.Automation.AutomationElement]::RootElement
    $procCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
    $nameCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "StillPoint")
    $and = New-Object System.Windows.Automation.AndCondition($procCond, $nameCond)
    $btnCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "CloseButton")

    for ($i = 0; $i -lt 6; $i++) {
        $win = $rootEl.FindFirst([System.Windows.Automation.TreeScope]::Children, $and)
        if ($null -ne $win) {
            $btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
            if ($null -ne $btn) {
                $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                Write-Output "settings window hidden via UIA"
                Start-Sleep -Milliseconds 700
                return
            }
        }
        Start-Sleep -Milliseconds 400
    }
    Write-Output "settings window could not be hidden"
}

Write-Output "== mode: $Mode =="
Hide-SettingsWindow

$fake = "$root\tools\FakeGame\bin\Release\net8.0-windows\FakeGame.exe"
$gameArgs = @()
if ($Mode -eq "windowed") { $gameArgs = @("--windowed") }
$sp = @{ FilePath = $fake; PassThru = $true }
if ($gameArgs.Count -gt 0) { $sp.ArgumentList = $gameArgs }
$game = Start-Process @sp
Start-Sleep -Seconds 6
Write-Output ("game pid=" + $game.Id + " exited=" + $game.HasExited)
Capture ("$root\analysis\shots\c_" + $Mode + "_overlay.png")

$statusPath = Join-Path $env:TEMP "stillpoint-fakegame.status.txt"
if (Test-Path $statusPath) {
    Write-Output "--- fakegame status (before click) ---"
    Get-Content $statusPath
}

if ($Mode -eq "fullscreen") {
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$root\tools\clicktest.ps1" -X 173 -Y 1312 -Clicks 2 | Write-Output
    Start-Sleep -Milliseconds 900
    Capture ("$root\analysis\shots\c_" + $Mode + "_after_click.png")
    if (Test-Path $statusPath) {
        Write-Output "--- fakegame status (after click) ---"
        Get-Content $statusPath
    }
}

$fg = [Compat]::GetForegroundWindow()
Write-Output ("foreground title length=" + [Compat]::GetWindowTextLength($fg))

[Compat]::keybd_event(0x1B, 0, 0, [UIntPtr]::Zero)
[Compat]::keybd_event(0x1B, 0, 2, [UIntPtr]::Zero)
Start-Sleep -Seconds 2
Write-Output ("game exited=" + $game.HasExited)

