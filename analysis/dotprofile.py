"""Radial profile analysis of the reference dots."""
import numpy as np
from PIL import Image

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
img = Image.open(SRC).convert("RGB")
a = np.asarray(img).astype(np.float64)
lum = a @ np.array([0.299, 0.587, 0.114])

cols = [448, 1002, 1556, 2110]
rows = [112, 517, 922, 1330]

def centroid(cx, cy, half=10):
    win = lum[cy - half:cy + half + 1, cx - half:cx + half + 1]
    m = win >= 250
    ys, xs = np.nonzero(m)
    if len(xs) == 0:
        return cx, cy, 0
    return cx - half + xs.mean(), cy - half + ys.mean(), len(xs)

def profile(cx, cy, rmax=30):
    yy, xx = np.mgrid[cy - rmax:cy + rmax + 1, cx - rmax:cx + rmax + 1]
    rr = np.hypot(xx - cx, yy - cy)
    out = []
    for r0 in range(0, rmax):
        m = (rr >= r0) & (rr < r0 + 1)
        if m.sum() == 0:
            continue
        out.append((r0, lum[cy - rmax:cy + rmax + 1, cx - rmax:cx + rmax + 1][m].mean()))
    return out

for j, cy in enumerate(rows):
    for i, cx in enumerate(cols):
        fx, fy, n = centroid(cx, cy)
        seg = profile(int(round(fx)), int(round(fy)))
        s = " ".join(f"{v:.0f}" for _, v in seg[:26])
        print(f"dot[{i},{j}] at ({cx},{cy}) centroid=({fx:.1f},{fy:.1f}) white_px={n}")
        print(f"   r0..25 meanL: {s}")
