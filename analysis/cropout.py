import sys
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
box = tuple(int(v) for v in sys.argv[3:7])  # left top right bottom (source px)
scale = float(sys.argv[7]) if len(sys.argv) > 7 else 1.0
img = Image.open(src).convert("RGB").crop(box)
if scale != 1.0:
    img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
img.save(out)
print(f"{out} {img.size}")
