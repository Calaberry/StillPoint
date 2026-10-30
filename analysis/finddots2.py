"""List only the small round white blobs that look like overlay dots."""
import sys
from collections import deque
from PIL import Image
import numpy as np

path = sys.argv[1] if len(sys.argv) > 1 else r"D:\dsh file\StillPoint\analysis\shots\p1_overlay.png"
minv = int(sys.argv[2]) if len(sys.argv) > 2 else 235
img = Image.open(path).convert("RGB")
a = np.asarray(img)
h, w, _ = a.shape
mask = a.min(axis=2) >= minv
seen = np.zeros((h, w), dtype=bool)
out = []
for y0, x0 in zip(*np.nonzero(mask)):
    if seen[y0, x0]:
        continue
    q = deque([(y0, x0)])
    seen[y0, x0] = True
    pts = []
    while q:
        y, x = q.popleft()
        pts.append((x, y))
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True
                q.append((ny, nx))
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    bw, bh = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
    if 6 <= len(pts) <= 120 and bw <= 16 and bh <= 16 and 0.6 <= bw / bh <= 1.6:
        out.append((sum(xs) / len(xs), sum(ys) / len(ys), len(pts), bw, bh))

out.sort(key=lambda b: (round(b[1] / 10), b[0]))
print(f"{len(out)} dot-like blobs in {path}")
for cx, cy, area, bw, bh in out:
    print(f"  ({cx:7.1f}, {cy:7.1f})  area={area:3d} box={bw}x{bh}")
