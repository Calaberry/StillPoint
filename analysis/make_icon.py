"""Turn the generated icon art into a transparent PNG master + multi-size .ico."""
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

SRC = r"C:\Users\calaberry\.dsh\dsh-codex-subscription\images\v1\bc\img_bc294d10137a6f0f5daa79c704806225\original"
OUT_DIR = Path(r"D:\dsh file\StillPoint\src\StillPoint\Resources")
OUT_DIR.mkdir(parents=True, exist_ok=True)

img = Image.open(SRC).convert("RGB")
arr = np.array(img).astype(np.int16)
h, w, _ = arr.shape

# Flood fill the near-white background from the border, so only the tile gets alpha.
near_white = (arr.min(axis=2) >= 244)
visited = np.zeros((h, w), dtype=bool)
queue = deque()
for x in range(w):
    for y in (0, h - 1):
        if near_white[y, x] and not visited[y, x]:
            visited[y, x] = True
            queue.append((y, x))
for y in range(h):
    for x in (0, w - 1):
        if near_white[y, x] and not visited[y, x]:
            visited[y, x] = True
            queue.append((y, x))
while queue:
    y, x = queue.popleft()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        ny, nx = y + dy, x + dx
        if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and near_white[ny, nx]:
            visited[ny, nx] = True
            queue.append((ny, nx))

rgba = np.dstack([arr.clip(0, 255).astype(np.uint8), np.where(visited, 0, 255).astype(np.uint8)])
icon = Image.fromarray(rgba, mode="RGBA")

# Tight crop to the tile, then a square canvas with a small even margin.
alpha = np.array(icon)[:, :, 3]
ys, xs = np.nonzero(alpha)
icon = icon.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
side = max(icon.size)
pad = int(side * 0.03)
canvas = Image.new("RGBA", (side + 2 * pad, side + 2 * pad), (0, 0, 0, 0))
canvas.paste(icon, ((canvas.width - icon.width) // 2, (canvas.height - icon.height) // 2), icon)

master = canvas.resize((1024, 1024), Image.LANCZOS)
master.save(OUT_DIR / "StillPoint.png")
print("png", master.size, master.mode)

sizes = [256, 128, 64, 48, 40, 32, 24, 20, 16]
frames = [master.resize((s, s), Image.LANCZOS) for s in sizes]
frames[0].save(
    OUT_DIR / "StillPoint.ico",
    format="ICO",
    sizes=[(s, s) for s in sizes],
    append_images=frames[1:],
)
print("ico written", [f.size for f in frames])

# A 32 px preview to eyeball legibility.
master.resize((32, 32), Image.LANCZOS).resize((256, 256), Image.NEAREST).save(
    r"D:\dsh file\StillPoint\analysis\shots\icon32_preview.png"
)
print("preview written")
