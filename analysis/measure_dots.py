import sys
from PIL import Image

path = sys.argv[1]
img = Image.open(path).convert("RGB")
out = []
for (cx, cy, label) in [(448, 111, "c0r0"), (1002, 517, "c1r1"), (2110, 1330, "c3r3")]:
    box = (cx - 16, cy - 16, cx + 16, cy + 16)
    crop = img.crop(box)
    px = crop.load()
    count = 0
    peak = (0, 0, 0)
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b = px[x, y]
            if min(r, g, b) >= 200:
                count += 1
            if r + g + b > sum(peak):
                peak = (r, g, b)
    out.append(f"{label}@({cx},{cy}) white_px={count} peak={peak}")

print("\n".join(out))
