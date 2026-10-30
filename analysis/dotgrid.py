"""Probe hypothesized 4x4 dot grid and build a contact sheet."""
import numpy as np
from PIL import Image, ImageDraw

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
img = Image.open(SRC).convert("RGB")
a = np.asarray(img).astype(np.int16)
H, W, _ = a.shape

cols = [448, 1002, 1556, 2110]
rows = [112, 517, 922, 1330]

for r in rows:
    line = []
    for c in cols:
        win = a[max(0,r-6):r+7, max(0,c-6):c+7]
        line.append(f"({c},{r}) max={win.max():3d} mean={win.mean():5.1f}")
    print(" | ".join(line))

# contact sheet: 4x4 crops 56x56 -> upscale 8x, 1px white border, label
CELL = 56
SC = 8
sheet = Image.new("RGB", (4 * (CELL * SC + 10), 4 * (CELL * SC + 10)), (10, 10, 10))
d = ImageDraw.Draw(sheet)
for j, r in enumerate(rows):
    for i, c in enumerate(cols):
        crop = img.crop((c - CELL // 2, r - CELL // 2, c + CELL // 2, r + CELL // 2))
        big = crop.resize((CELL * SC, CELL * SC), Image.NEAREST)
        x = i * (CELL * SC + 10) + 5
        y = j * (CELL * SC + 10) + 5
        sheet.paste(big, (x, y))
        d.rectangle([x, y, x + CELL * SC, y + CELL * SC], outline=(0, 220, 255))
        d.text((x + 4, y + 2), f"{c},{r}", fill=(255, 60, 60))
sheet.save(r"D:\dsh file\StillPoint\analysis\dotgrid.png")
print("sheet", sheet.size)
