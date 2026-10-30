"""Fifth pass: remaining shop signage."""
from PIL import Image, ImageEnhance, ImageFilter
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops5"
os.makedirs(OUT, exist_ok=True)
img = Image.open(SRC).convert("RGB")

regions = {
    "s_house_sign":  (170, 430, 620, 680, 5),
    "s_redframe":    (1120, 450, 1360, 680, 8),
    "s_right_mid2":  (1600, 100, 2020, 560, 4),
    "s_center_sw":   (1040, 100, 1640, 720, 3),
}

for name, (l, t, r, b, scale) in regions.items():
    c = img.crop((l, t, r, b))
    c = c.resize((c.width * scale, c.height * scale), Image.LANCZOS)
    c = ImageEnhance.Contrast(c).enhance(1.35)
    c = ImageEnhance.Brightness(c).enhance(1.3)
    c = c.filter(ImageFilter.UnsharpMask(radius=2, percent=180, threshold=2))
    c.save(os.path.join(OUT, name + ".png"))
    print(name, c.size)
