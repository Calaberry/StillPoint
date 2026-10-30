import io
p = r"D:\dsh file\StillPoint\analysis\final_shots.ps1"
s = io.open(p, encoding="utf-8").read()
old = """$sp = $win.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
$sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)"""
new = """$scond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$scrollers = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $scond)
Write-Output ("scrollable elements: " + $scrollers.Count)
if ($scrollers.Count -gt 0) {
    $sp = $scrollers[0].GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 100)
    Write-Output "scrolled"
}"""
assert old in s
s = s.replace(old, new)
io.open(p, "w", encoding="utf-8").write(s)
print("patched")
