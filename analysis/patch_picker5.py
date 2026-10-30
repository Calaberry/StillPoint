import io
p = r"D:\dsh file\StillPoint\analysis\test_picker.ps1"
s = io.open(p, encoding="ascii").read()
s = s.replace("$py = [int]($r.Top + 60)", "$clickY = [int]($r.Top + 60)")
s = s.replace("-X $px -Y $py", "-X $px -Y $clickY")
io.open(p, "w", encoding="ascii").write(s)
print("patched")
