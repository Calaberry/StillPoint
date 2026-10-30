"""Detect the anti-motion-sickness white dots in the reference screenshot."""
import numpy as np
from PIL import Image, ImageFilter

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
img = Image.open(SRC).convert("RGB")
a = np.asarray(img).astype(np.int16)
H, W, _ = a.shape
print("size", W, H)

r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
mn = np.minimum(np.minimum(r, g), b)

for thr in (250, 245, 240, 235):
    mask = mn >= thr
    print("thr", thr, "px", int(mask.sum()))

mask = mn >= 240
# label connected components
seen = np.zeros_like(mask, dtype=bool)
ys, xs = np.nonzero(mask)
comps = []
for y0, x0 in zip(ys, xs):
    if seen[y0, x0]:
        continue
    stack = [(y0, x0)]
    seen[y0, x0] = True
    pix = []
    while stack:
        y, x = stack.pop()
        pix.append((y, x))
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                yy, xx = y + dy, x + dx
                if 0 <= yy < H and 0 <= xx < W and mask[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    stack.append((yy, xx))
    py = np.array([p[0] for p in pix]); px = np.array([p[1] for p in pix])
    comps.append((len(pix), int(px.min()), int(py.min()), int(px.max()), int(py.max())))

print("components", len(comps))
cand = [c for c in comps if 3 <= c[0] <= 120]
print("candidates", len(cand))
cand.sort(key=lambda c: (c[1], c[2]))
for n, l, t, rr, bb in cand:
    print(f"area={n:4d} box=({l},{t})-({rr},{bb}) wh=({rr-l+1},{bb-t+1}) cx={(l+rr)/2:.0f} cy={(t+bb)/2:.0f}")
