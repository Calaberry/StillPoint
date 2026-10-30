"""Cross-check that every resource key referenced from XAML exists in both theme dictionaries."""
import re
from pathlib import Path

ROOT = Path(r"D:\dsh file\StillPoint\src\StillPoint")
KEY = re.compile(r'x:Key="([^"]+)"')
REF = re.compile(r'\{(?:Static|Dynamic)Resource ([A-Za-z0-9_.]+)\}')


def keys(xaml: Path) -> set[str]:
    return set(KEY.findall(xaml.read_text(encoding="utf-8")))


light = keys(ROOT / "Styles" / "Theme.xaml")
dark = keys(ROOT / "Styles" / "ThemeDark.xaml")
controls = keys(ROOT / "Styles" / "Controls.xaml")

print("light keys :", len(light))
print("dark keys  :", len(dark))
print("only light :", sorted(light - dark))
print("only dark  :", sorted(dark - light))

# Keys defined in Controls.xaml (styles) plus theme keys.
defined = light | controls
missing = {}
for xaml in ROOT.rglob("*.xaml"):
    if xaml.name in {"Theme.xaml", "ThemeDark.xaml"}:
        continue
    text = xaml.read_text(encoding="utf-8")
    for ref in REF.findall(text):
        if ref not in defined:
            missing.setdefault(xaml.name, set()).add(ref)

if missing:
    for name, refs in missing.items():
        print(f"UNDEFINED in {name}: {sorted(refs)}")
else:
    print("all referenced keys resolve")

# For theme-swapped keys: which ones does each other file reference?
swapped = {r for xaml in ROOT.rglob("*.xaml") if xaml.name not in {"Theme.xaml", "ThemeDark.xaml"}
           for r in REF.findall(xaml.read_text(encoding="utf-8"))}
print("keys referenced outside theme dicts:", len(swapped))
print("theme keys never referenced:", sorted((light - swapped)))
