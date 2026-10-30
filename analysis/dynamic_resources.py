"""Switch brush references from StaticResource to DynamicResource so the palette can be swapped live."""
import re
from pathlib import Path

ROOT = Path(r"D:\dsh file\StillPoint\src\StillPoint")
FILES = ["Styles/Controls.xaml", "Views/SettingsWindow.xaml", "Controls/ColorPicker.xaml"]

pattern = re.compile(r"\{StaticResource ((\w+)Brush)\}")

for rel in FILES:
    path = ROOT / rel
    text = path.read_text(encoding="utf-8")
    new, count = pattern.subn(lambda m: "{DynamicResource " + m.group(1) + "}", text)
    path.write_text(new, encoding="utf-8")
    left = len(pattern.findall(new))
    print(f"{rel}: replaced {count}, remaining {left}")
