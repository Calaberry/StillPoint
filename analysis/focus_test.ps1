# Focus / z-order robustness: another app in front, Alt+Tab both ways, overlay must keep drawing and never steal input.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class FocusNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    public static string Title() { var sb = new StringBuilder(512); GetWindowText(GetForegroundWindow(), sb, sb.Capacity); return sb.ToString(); }
    public static string Class() { var sb = new StringBuilder(256); GetClassName(GetForegroundWindow(), sb, sb.Capacity); return sb.ToString(); }
}
'@
[void][FocusNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$screenW = [FocusNative]::GetSystemMetrics(0)
$screenH = [FocusNative]::GetSystemMetrics(1)
$py = 'C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$measure = 'D:\dsh file\StillPoint\analysis\measure3.py'
$shots = 'D:\dsh file\StillPoint\analysis\shots\focus'
New-Item -ItemType Directory -Force -Path $shots | Out-Null

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

function Dots([string]$png) {
    $out = & $py $measure $png 1.0 1.0 1.0 2560
    return (($out | Select-String -Pattern '^SUMMARY').Line)
}

function AltTab {
    [FocusNative]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)
    [FocusNative]::keybd_event(0x09, 0, 0, [IntPtr]::Zero)
    [FocusNative]::keybd_event(0x09, 0, 2, [IntPtr]::Zero)
    [FocusNative]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
}

$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name notepad -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
$proc = Start-Process -FilePath 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe' -PassThru
Start-Sleep -Seconds 4

Write-Output ("A foreground class=" + [FocusNative]::Class() + " title=" + [FocusNative]::Title())
Write-Output ("A dots: " + (Dots (Capture 'f1_settings_focused.png')))

$note = Start-Process notepad -PassThru
Start-Sleep -Seconds 3
Write-Output ("B foreground class=" + [FocusNative]::Class() + " title=" + [FocusNative]::Title())
Write-Output ("B dots: " + (Dots (Capture 'f2_notepad_focused.png')))

AltTab
Start-Sleep -Seconds 2
Write-Output ("C foreground class=" + [FocusNative]::Class() + " title=" + [FocusNative]::Title())
Write-Output ("C dots: " + (Dots (Capture 'f3_after_alttab.png')))

AltTab
Start-Sleep -Seconds 2
Write-Output ("D foreground class=" + [FocusNative]::Class() + " title=" + [FocusNative]::Title())
Write-Output ("D dots: " + (Dots (Capture 'f4_after_alttab2.png')))

# the overlay must not appear in Alt+Tab / taskbar; check the window list of our process
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
Write-Output ("our top-level UIA windows: " + $wins.Count + " -> " + (($wins | ForEach-Object { $_.Current.Name }) -join ' | '))

Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Id $note.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Output 'DONE'
