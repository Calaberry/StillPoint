import io, re
p = r"D:\dsh file\StillPoint\src\StillPoint\Views\SettingsWindow.xaml"
s = io.open(p, encoding="utf-8").read()
names = {
    "DotSizeSlider": "点阵大小",
    "GlowSlider": "光晕强度",
    "OpacitySlider": "不透明度",
    "ScaleSlider": "整体缩放",
    "SpacingSlider": "间距",
    "SpacingHSlider": "水平间距",
    "SpacingVSlider": "垂直间距",
}
for key, label in names.items():
    anchor = '<Slider x:Name="%s"' % key
    assert anchor in s, key
    s = s.replace(anchor, '<Slider x:Name="%s" AutomationProperties.Name="%s"' % (key, label), 1)
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("patched sliders")
