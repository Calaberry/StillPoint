import io
p = r"D:\dsh file\StillPoint\analysis\verify_dist.ps1"
s = io.open(p, encoding="utf-8").read()
s = s.replace("dist\\StillPoint-portable\\StillPoint.exe", "dist\\StillPoint\\StillPoint.exe")
s = s.replace("'D:\\dsh file\\StillPoint\\dist\\StillPoint\\StillPoint.exe';          Root = $null }", "'D:\\dsh file\\StillPoint\\dist\\StillPoint-small\\StillPoint.exe';    Root = $null }")
io.open(p, "w", encoding="utf-8").write(s)
print([l for l in s.splitlines() if "Exe =" in l])
