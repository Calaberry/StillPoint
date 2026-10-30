import io, re
p = r"D:\dsh file\StillPoint\analysis\test_picker.ps1"
s = io.open(p, encoding="utf-8", errors="replace").read()
s2 = s.replace('[System.Windows.Automation.AutomationElement]::NameProperty, "\u81ea\u9009\u2026"',
               '[System.Windows.Automation.AutomationElement]::NameProperty, $customCaption')
s2 = s2.replace('[System.Windows.Automation.AutomationElement]::NameProperty, "\u5b8c\u6210"',
                '[System.Windows.Automation.AutomationElement]::NameProperty, $doneCaption')
io.open(p, "w", encoding="ascii", errors="strict").write(s2)
print("patched", s != s2)
