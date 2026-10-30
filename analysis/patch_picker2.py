import io
p = r"D:\dsh file\StillPoint\analysis\test_picker.ps1"
lines = io.open(p, encoding="utf-8", errors="replace").read().splitlines()
out = []
for ln in lines:
    if "$btnCond = New-Object" in ln:
        out.append(ln)
        out.append("    [System.Windows.Automation.AutomationElement]::NameProperty, $customCaption)")
        continue
    if "NameProperty, \"??????" in ln or ln.strip().startswith("[System.Windows.Automation.AutomationElement]::NameProperty, \"?") or "NameProperty, $customCaption)" in ln or ln.strip() == ')' :
        continue
    if "$doneCond = New-Object" in ln:
        out.append(ln)
        out.append("    [System.Windows.Automation.AutomationElement]::NameProperty, $doneCaption)")
        continue
    out.append(ln)
text = "\n".join(out) + "\n"
io.open(p, "w", encoding="ascii", errors="strict").write(text)
print("lines", len(lines), "->", len(out))
