"""Find small near-white blobs (the overlay dots) in a captured desktop shot."""
import sys
from PIL import Image
import numpy as np

path = sys.argv[1] if len(sys.argv) > 1 else r"D:\dsh file\StillPoint\analysis\shots\p1_overlay.png"
img = Image.open(path).convert("RGB")
a = np.asarray(img)
h, w, _ = a.shape
mask = (a.min(axis=2) >= 235)
# connected components via simple flood fill (4-neighbour) using scipy-free BFS
lbl = np.zeros((h, w), dtype=np.int32)
cur = 0
blobs = []
ys, xs = np.nonzero(mask)
seen = np.zeros((h, w), dtype=bool)
from collections import deque
for y0, x0 in zip(ys, xs):
    if seen[y0, x0]:
        continue
    cur += 1
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
    if 4 <= len(pts) <= 200:
        px = [p[0] for p in pts]
        py = [p[1] for p in pts]
        blobs.append((sum(px) / len(px), sum(py) / len(py), len(pts), max(px) - min(px) + 1, max(py) - min(py) + 1))

print(f"image {w}x{h}, candidates: {len(blobs)}")
blobs.sort(key=lambda b: (round(b[1] / 6), b[0]))
for cx, cy, area, bw, bh in blobs:
    print(f"  ({cx:7.1f}, {cy:7.1f})  area={area:3d}  box={bw}x{bh}")
