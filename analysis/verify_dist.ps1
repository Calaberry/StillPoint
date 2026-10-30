# Verify each published variant: start, render 16 dots, measure startup time, then stop.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class VerNative {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][VerNative]::SetProcessDpiAwarenessContext([IntPtr](-4))
$screenW = [VerNative]::GetSystemMetrics(0)
$screenH = [VerNative]::GetSystemMetrics(1)
$py = 'C:\Users\calaberry\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe'
$measure = 'D:\dsh file\StillPoint\analysis\measure3.py'
$shots = 'D:\dsh file\StillPoint\analysis\shots'
$log = Join-Path $env:LOCALAPPDATA 'StillPoint\logs\stillpoint.log'

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

$variants = @(
    @{ Name = 'StillPoint';      Exe = 'D:\dsh file\StillPoint\dist\StillPoint\StillPoint.exe';            Runtime = $false },
    @{ Name = 'StillPoint-small';Exe = 'D:\dsh file\StillPoint\dist\StillPoint-small\StillPoint.exe';      Runtime = $false },
    @{ Name = 'StillPoint-lite'; Exe = 'D:\dsh file\StillPoint\dist\StillPoint-lite\StillPoint.exe';       Runtime = $true  }
)

foreach ($v in $variants) {
    Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 700
    Remove-Item (Join-Path $env:LOCALAPPDATA 'StillPoint\settings.json') -ErrorAction SilentlyContinue
    Remove-Item $log -ErrorAction SilentlyContinue
    if ($v.Runtime) {
        $env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
        $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    } else {
        Remove-Item Env:DOTNET_ROOT -ErrorAction SilentlyContinue
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $proc = Start-Process -FilePath $v.Exe -PassThru
    $startup = -1.0
    for ($i = 0; $i -lt 100; $i++) {
        Start-Sleep -Milliseconds 100
        if (Test-Path $log) {
            if ((Get-Content $log -Raw -ErrorAction SilentlyContinue) -match 'Startup complete') {
                $startup = $sw.Elapsed.TotalSeconds
                break
            }
        }
        if ($proc.HasExited) { break }
    }
    $sw.Stop()
    Start-Sleep -Milliseconds 1200
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    $ws = if ($p) { [math]::Round($p.WorkingSet64 / 1MB, 1) } else { -1 }
    $png = Capture ("dist_{0}.png" -f $v.Name)
    $out = & $py $measure $png 1.0 1.0 1.0 2560
    $summary = ($out | Select-String -Pattern '^SUMMARY').Line
    Write-Output ("{0}: startup={1:N2}s ws={2}MB  {3}" -f $v.Name, $startup, $ws, $summary)
    Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}
Write-Output 'DONE'
