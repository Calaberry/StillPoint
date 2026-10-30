"""Measure the StillPoint overlay dots at their expected lattice positions."""
import sys

import numpy as np
from PIL import Image

BASE_X = [448.7, 1002.7, 1556.7, 2110.7]
BASE_Y = [110.7, 516.7, 922.7, 1328.7]
CX, CY = 1280.0, 720.0


def luminance(a):
    return 0.2126 * a[:, :, 0] + 0.7152 * a[:, :, 1] + 0.0722 * a[:, :, 2]


def report(path, scale=1.0, spacing=1.0):
    img = Image.open(path).convert("RGB")
    arr = np.array(img).astype(np.float64)
    lum = luminance(arr)
    print(f"== {path}  (scale {scale}, spacing {spacing})")
    xs = [CX + (x - CX) * scale * spacing for x in BASE_X]
    ys = [CY + (y - CY) * scale * spacing for y in BASE_Y]
    print("   expected x:", [round(x, 1) for x in xs])
    print("   expected y:", [round(y, 1) for y in ys])
    brightest = []
    sizes = []
    for row, ey in enumerate(ys):
        for column, ex in enumerate(xs):
            x0, x1 = int(ex) - 10, int(ex) + 11
            y0, y1 = int(ey) - 10, int(ey) + 11
            patch = lum[y0:y1, x0:x1]
            peak = patch.max()
            py, px = np.unravel_index(int(np.argmax(patch)), patch.shape)
            brightest.append(peak)
            if peak >= 170:
                mask = patch >= 170
                sizes.append(int(mask.sum()))
                if row == 1 and column == 0:
                    colour = arr[y0 + py, x0 + px]
                    print(f"   peak colour at dot(1,0): {tuple(int(v) for v in colour)}")
            else:
                sizes.append(0)
    print("   peak luminance per dot:", [int(v) for v in brightest])
    print("   bright pixel count per dot:", sizes)
    print("   dots with peak>=170:", sum(1 for v in brightest if v >= 170))


if __name__ == "__main__":
    args = sys.argv[1:]
    if not args:
        print("usage: measure2.py <png> [scale]")
        raise SystemExit(1)
    scale = float(args[1]) if len(args) > 1 else 1.0
    report(args[0], scale)
