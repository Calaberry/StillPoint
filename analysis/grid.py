"""Grid overlay to locate features precisely, then band crops."""
from PIL import Image, ImageDraw, ImageFont

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis"

img = Image.open(SRC).convert("RGB")
W, H = img.size
small = img.resize((1280, 720), Image.LANCZOS)
d = ImageDraw.Draw(small)
try:
    font = ImageFont.truetype("arial.ttf", 13)
except Exception:
    font = ImageFont.load_default()
k = W / 1280.0
for x in range(0, W + 1, 100):
    sx = int(x / k)
    d.line([(sx, 0), (sx, 719)], fill=(0, 255, 0), width=1)
    if x % 200 == 0:
        d.text((sx + 1, 1), str(x), fill=(0, 255, 0), font=font)
for y in range(0, H + 1, 100):
    sy = int(y / k)
    d.line([(0, sy), (1279, sy)], fill=(0, 255, 0), width=1)
    if y % 200 == 0:
        d.text((1, sy), str(y), fill=(0, 255, 0), font=font)
small.save(OUT + r"\grid_overview.png")
print("grid saved")
