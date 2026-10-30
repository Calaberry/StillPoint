Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Cap4 {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
  [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
}
"@
[Cap4]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$script:W = [Cap4]::GetSystemMetrics(0)
$script:H = [Cap4]::GetSystemMetrics(1)

function Capture($path) {
    $bmp = New-Object System.Drawing.Bitmap $script:W, $script:H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

$proc = Get-Process -Name StillPoint -ErrorAction Stop | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::RootElement
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
if ($null -eq $win) { Write-Output "WINDOW NOT FOUND"; exit 1 }
Write-Output ("window: " + $win.Current.Name + " class=" + $win.Current.ClassName)

function Find-ById($id) {
    $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}

$slider = Find-ById "DotSizeSlider"
if ($null -eq $slider) { Write-Output "SLIDER NOT FOUND"; exit 1 }
$rv = $slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
Write-Output ("dot size before: " + $rv.Current.Value)
$rv.SetValue(2.0)
Start-Sleep -Milliseconds 600
Capture "D:\dsh file\StillPoint\analysis\shots\p7_dotsize_200.png"
Write-Output ("dot size after: " + $rv.Current.Value)

# colour: pick the "薄荷" swatch
$sw = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::RadioButton)))
foreach ($s in $sw) { Write-Output ("radio: " + $s.Current.Name + " | id=" + $s.Current.AutomationId) }
