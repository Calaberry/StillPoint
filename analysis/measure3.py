"""Measure dot peak, near halo (r 4.2-6.5) and far ring (r 7-12) at the expected lattice positions."""
import sys
import numpy as np
from PIL import Image

BASE_X = [448.7, 1002.7, 1556.7, 2110.7]
BASE_Y = [110.7, 516.7, 922.7, 1328.7]
CX, CY = 1280.0, 720.0


def mean_at(img: np.ndarray, x: float, y: float, radii) -> float:
    vals = []
    for r in radii:
        for k in range(12):
            ang = k * np.pi / 6.0
            px, py = int(round(x + r * np.cos(ang))), int(round(y + r * np.sin(ang)))
            if 0 <= py < img.shape[0] and 0 <= px < img.shape[1]:
                vals.append(int(img[py, px]))
    return float(np.mean(vals)) if vals else float('nan')


def main(path: str, scale: float = 1.0, spacing_h: float = 1.0, spacing_v: float = 1.0, max_x: float = 1e9) -> None:
    img = np.array(Image.open(path).convert("RGB")).astype(np.int32).min(axis=2)
    h, w = img.shape
    print(f"IMAGE {path} {w}x{h}")
    hot, halo, ring, counted = 0, [], [], 0
    for row, by in enumerate(BASE_Y):
        for col, bx in enumerate(BASE_X):
            x = CX + (bx - CX) * scale * spacing_h
            y = CY + (by - CY) * scale * spacing_v
            cx = min(max(x, 14), w - 15)
            cy = min(max(y, 14), h - 15)
            xi, yi = int(round(cx)), int(round(cy))
            peak = int(img[max(0, yi - 2):yi + 3, max(0, xi - 2):xi + 3].max())
            near = mean_at(img, cx, cy, (4.2, 4.8, 5.4, 6.0))
            far = mean_at(img, cx, cy, (7.0, 8.5, 10.0, 11.5))
            inside = x <= max_x
            print(f"  ({col},{row}) x={x:7.1f} y={y:7.1f} peak={peak:3d} near={near:6.1f} far={far:6.1f}"
                  + ("" if inside else "  [covered]"))
            if inside:
                counted += 1
                hot += 1 if peak >= 170 else 0
                halo.append(near)
                ring.append(far)
    print(f"SUMMARY counted={counted} dots_with_peak>=170: {hot}/{counted} "
          f"mean_near={np.mean(halo):.1f} mean_far={np.mean(ring):.1f}")


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        raise SystemExit("usage: measure3.py <png> [scale] [spacingH] [spacingV] [maxX]")
    main(args[0], *(float(a) for a in args[1:5]))
