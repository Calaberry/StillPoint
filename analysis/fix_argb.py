from pathlib import Path
root = Path(r"D:\dsh file\StillPoint\src\StillPoint\Styles")
fixes = {
    "Theme.xaml": {"#00000010": "#10000000", "#0000000F": "#0F000000", "#2C7BE51F": "#1F2C7BE5"},
    "ThemeDark.xaml": {"#FFFFFF14": "#14FFFFFF", "#FFFFFF12": "#12FFFFFF", "#4B90F02E": "#2E4B90F0"},
}
for name, table in fixes.items():
    p = root / name
    text = p.read_text(encoding="utf-8")
    for old, new in table.items():
        assert old in text, (name, old)
        text = text.replace(old, new)
    p.write_text(text, encoding="utf-8")
    print("fixed", name)
