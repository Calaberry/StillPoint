"""Sixth pass: rotate the angled white sign at the top right."""
from PIL import Image, ImageEnhance, ImageFilter
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops6"
os.makedirs(OUT, exist_ok=True)
img = Image.open(SRC).convert("RGB")

for name, box, angle, scale in [
    ("t_topright_sign", (1830, 70, 2080, 240), -32, 7),
    ("t_aguijjim", (1270, 45, 1440, 290), 90, 6),
]:
    c = img.crop(box).rotate(angle, expand=True, resample=Image.BICUBIC)
    c = c.resize((c.width * scale, c.height * scale), Image.LANCZOS)
    c = ImageEnhance.Contrast(c).enhance(1.35)
    c = ImageEnhance.Brightness(c).enhance(1.15)
    c = c.filter(ImageFilter.UnsharpMask(radius=2, percent=160, threshold=3))
    c.save(os.path.join(OUT, name + ".png"))
    print(name, c.size)
