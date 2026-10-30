"""Locate the 16 overlay dots in a screenshot and report position + colour."""
import sys
from collections import deque, Counter

import numpy as np
from PIL import Image


def blobs(path):
    img = Image.open(path).convert("RGB")
    arr = np.array(img).astype(np.int16)
    h, w, _ = arr.shape
    mn = arr.min(axis=2)
    mx = arr.max(axis=2)
    mask = (mn >= 130) & ((mx - mn) <= 45)
    seen = np.zeros((h, w), dtype=bool)
    out = []
    for y in range(h):
        for x in range(w):
            if not mask[y, x] or seen[y, x]:
                continue
            q = deque([(y, x)])
            seen[y, x] = True
            pts = []
            while q:
                cy, cx = q.popleft()
                pts.append((cy, cx))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            q.append((ny, nx))
            if not (10 <= len(pts) <= 400):
                continue
            ys = [p[0] for p in pts]
            xs = [p[1] for p in pts]
            bh, bw = max(ys) - min(ys) + 1, max(xs) - min(xs) + 1
            if bh > 22 or bw > 22:
                continue
            if not (0.5 <= bw / bh <= 2.0):
                continue
            cy = sum(ys) / len(ys)
            cx = sum(xs) / len(xs)
            out.append((cx, cy, len(pts), bw, bh))
    return out, arr


def main(paths):
    for path in paths:
        found, arr = blobs(path)
        print(f"== {path}")
        print(f"   candidates: {len(found)}")
        # keep the 16 blobs closest to a 4x4 lattice by simply taking the largest-area 16
        found.sort(key=lambda b: -b[2])
        top = found[:16]
        cols = Counter(round(b[0] / 10) for b in top)
        rows = Counter(round(b[1] / 10) for b in top)
        print("   x columns:", sorted({round(b[0]) for b in top}))
        print("   y rows:   ", sorted({round(b[1]) for b in top}))
        if top:
            x, y = int(round(top[0][0])), int(round(top[0][1]))
            y0, y1 = max(0, y - 3), min(arr.shape[0], y + 4)
            x0, x1 = max(0, x - 3), min(arr.shape[1], x + 4)
            patch = arr[y0:y1, x0:x1].reshape(-1, 3)
            print("   centre pixel:", tuple(int(v) for v in np.median(patch, axis=0)))
            print("   areas:", sorted(b[2] for b in top))


if __name__ == "__main__":
    main(sys.argv[1:])
