# 5-minute soak: launch, hide the settings window (overlay only), sample every 15 s.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$out = 'D:\dsh file\StillPoint\analysis\soak.txt'
$env:DOTNET_ROOT = 'D:\dsh file\StillPoint\.dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$exe = 'D:\dsh file\StillPoint\src\StillPoint\bin\Debug\net8.0-windows\StillPoint.exe'

Get-Process -Name StillPoint -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5

# hide the settings window so we measure the overlay-only steady state
$root = [System.Windows.Automation.AutomationElement]::RootElement
$c1 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$c2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'StillPoint')
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.AndCondition($c1, $c2)))
if ($win) {
    $cc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'CloseButton')
    $close = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cc)
    if ($close) { $close.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
Start-Sleep -Seconds 2

$p0 = Get-Process -Id $proc.Id
$cpu0 = $p0.TotalProcessorTime.TotalSeconds
"pid=$($proc.Id) start=$((Get-Date).ToString('HH:mm:ss')) settings_window_hidden" | Set-Content $out
for ($i = 1; $i -le 20; $i++) {
    Start-Sleep -Seconds 15
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if (-not $p) { "t=$($i*15)s PROCESS GONE" | Add-Content $out; break }
    $cpu = [math]::Round($p.TotalProcessorTime.TotalSeconds - $cpu0, 3)
    ("t={0,3}s cpu_total={1,7:N3}s ws={2,7:N1}MB priv={3,7:N1}MB handles={4} threads={5}" -f ($i*15), $cpu, ($p.WorkingSet64/1MB), ($p.PrivateMemorySize64/1MB), $p.HandleCount, $p.Threads.Count) | Add-Content $out
}
Start-Sleep -Seconds 30
$p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($p) {
    ("idle+30s cpu={0:N3}s ws={1:N1}MB" -f ($p.TotalProcessorTime.TotalSeconds - $cpu0), ($p.WorkingSet64/1MB)) | Add-Content $out
}
"done $((Get-Date).ToString('HH:mm:ss'))" | Add-Content $out
Get-Process -Id $proc.Id -ErrorAction SilentlyContinue | Stop-Process -Force
