"""Fourth pass: locate remaining signage."""
from PIL import Image, ImageEnhance, ImageFilter
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops4"
os.makedirs(OUT, exist_ok=True)
img = Image.open(SRC).convert("RGB")

regions = {
    "q_red_banner_br": (1950, 880, 2420, 1210, 4),
    "q_menu_sign":     (1080, 380, 1380, 640, 5),
    "q_center_neon":   (1300, 280, 1560, 720, 5),
    "q_left_shop":     (0,   300, 420, 820, 4),
    "q_right_posters": (1980, 180, 2470, 700, 3),
    "q_right_mid":     (1740, 130, 2050, 520, 4),
    "q_stall_green":   (1180, 420, 1500, 660, 4),
}

for name, (l, t, r, b, scale) in regions.items():
    c = img.crop((l, t, r, b))
    c = c.resize((c.width * scale, c.height * scale), Image.LANCZOS)
    c = ImageEnhance.Contrast(c).enhance(1.3)
    c = ImageEnhance.Brightness(c).enhance(1.25)
    c = c.filter(ImageFilter.UnsharpMask(radius=2, percent=160, threshold=3))
    c.save(os.path.join(OUT, name + ".png"))
    print(name, c.size)
