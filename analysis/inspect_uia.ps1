# Inspect the settings window's UIA tree: ids, control types, names.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$exe = 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5

$root = [System.Windows.Automation.AutomationElement]::RootElement
$c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$wins = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $c1)
Write-Output ("windows for pid: " + $wins.Count)
foreach ($w in $wins) { Write-Output ("  name='" + $w.Current.Name + "' class=" + $w.Current.ClassName) }

$win = $wins | Where-Object { $_.Current.Name -eq 'StillPoint' } | Select-Object -First 1
if (-not $win) { throw 'no settings window' }
$all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
Write-Output ("descendants: " + $all.Count)
foreach ($e in $all) {
    $id = $e.Current.AutomationId
    if ($id -like 'Theme*' -or $id -like 'Backdrop*' -or $e.Current.ControlType.ProgrammaticName -eq 'ControlType.RadioButton') {
        Write-Output ("  id='" + $id + "' type=" + $e.Current.ControlType.ProgrammaticName + " name='" + $e.Current.Name + "'")
    }
}
Write-Output 'DONE'
