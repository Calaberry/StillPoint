import io
p = r"D:\dsh file\StillPoint\analysis\test_picker.ps1"
s = io.open(p, encoding="ascii").read()
old = """$btnCond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)"""
new = """$btnCond = New-Object System.Windows.Automation.AndCondition(
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)),
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)))"""
assert old in s
s = s.replace(old, new)
io.open(p, "w", encoding="ascii").write(s)
print("patched")
