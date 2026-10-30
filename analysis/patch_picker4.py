import io
p = r"D:\dsh file\StillPoint\analysis\test_picker.ps1"
s = io.open(p, encoding="ascii").read()
old_start = s.index("$btnCond = New-Object")
old_end = s.index("$inv.Invoke()")
old = s[old_start:old_end]
new = """$btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)
$label = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $label) { throw "custom colour label not found" }
$btn = $label
$walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
while ($btn -and $btn.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) {
    $btn = $walker.GetParent($btn)
}
if (-not $btn) { throw "custom colour button not found" }
$inv = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
"""
s = s[:old_start] + new + s[old_end:]
io.open(p, "w", encoding="ascii").write(s)
print("patched")
