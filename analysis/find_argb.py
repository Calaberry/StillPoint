import re
from pathlib import Path
root = Path(r"D:\dsh file\StillPoint\src\StillPoint")
pat = re.compile(r'#[0-9A-Fa-f]{8}\b')
for p in sorted(root.rglob("*")):
    if p.suffix.lower() not in {".xaml", ".cs"}:
        continue
    for i, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
        for m in pat.findall(line):
            print(f"{p.relative_to(root)}:{i}: {m}  | {line.strip()[:90]}")
