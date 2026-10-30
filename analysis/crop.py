"""Crop + upscale regions of the StillPoint reference screenshot for text OCR-by-eye."""
from PIL import Image, ImageEnhance
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops"
os.makedirs(OUT, exist_ok=True)

img = Image.open(SRC).convert("RGB")
W, H = img.size
print("source", W, H)

# name: (left, top, right, bottom) in 2560x1440 coordinates
regions = {
    "01_sign_red":        (340,   0,  870, 160),
    "02_sign_seafood":    (200, 315,  430, 425),
    "03_shop_left":       (400, 410,  560, 500),
    "04_sign_vertical":   (1120, 240, 1220, 410),
    "05_sign_yellow":     (1290,  70, 1410, 210),
    "06_sign_white":      (1590, 140, 1700, 280),
    "07_sign_topright":   (1830,  30, 1990, 160),
    "08_menu_board":      (2000, 320, 2410, 470),
    "09_menu_posters":    (1980, 415, 2440, 520),
    "10_subtitle":        (850, 1110, 1900, 1200),
    "11_nametag":         (1300, 650, 1500, 730),
    "12_hud_top":         (1060,   0, 1670,  34),
    "13_sticker_chicken": (1630, 190, 1790, 340),
    "14_sticker_octopus": (900,  160, 1040, 320),
    "15_menu_sign":       (1100, 420, 1230, 480),
    "16_sign_reddish":    (2100, 810, 2440, 915),
    "17_left_top":        (0,    20,   60, 180),
    "18_midleft_posters": (390, 380,  480, 470),
    "19_center_wall":     (1300, 300, 1420, 470),
    "20_right_top_sign":  (1650, 100, 1800, 220),
}

for name, box in regions.items():
    crop = img.crop(box)
    # upscale so the smaller dimension is at least ~420 px
    scale = max(2, min(6, int(420 / max(1, min(crop.size)))))
    big = crop.resize((crop.width * scale, crop.height * scale), Image.LANCZOS)
    big = ImageEnhance.Contrast(big).enhance(1.15)
    big = ImageEnhance.Sharpness(big).enhance(1.6)
    path = os.path.join(OUT, name + ".png")
    big.save(path)
    print(name, crop.size, "->", big.size)
