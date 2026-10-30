"""Third pass: tight crops + rotated reads for angled signs."""
from PIL import Image, ImageEnhance, ImageFilter
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops3"
os.makedirs(OUT, exist_ok=True)

img = Image.open(SRC).convert("RGB")


def save(name, box, scale=6, rotate=0, crop_after=None):
    c = img.crop(box)
    if rotate:
        c = c.rotate(rotate, expand=True, resample=Image.BICUBIC)
    if crop_after:
        c = c.crop(crop_after)
    c = c.resize((c.width * scale, c.height * scale), Image.LANCZOS)
    c = ImageEnhance.Contrast(c).enhance(1.3)
    c = ImageEnhance.Brightness(c).enhance(1.2)
    c = c.filter(ImageFilter.UnsharpMask(radius=2, percent=180, threshold=2))
    c.save(os.path.join(OUT, name + ".png"))
    print(name, c.size)


save("r_topright_rot", (1830, 40, 2070, 200), scale=6, rotate=-38)
save("r_whitevert_tight", (1570, 115, 1740, 320), scale=8)
save("r_leftedge_vert", (0, 30, 70, 260), scale=10)
save("r_vert_neon", (1130, 230, 1260, 440), scale=7)
save("r_menu_sign_tight", (1100, 415, 1260, 495), scale=8)
save("r_left_shop_signs", (380, 380, 640, 560), scale=5)
save("r_shop_door_sign", (420, 300, 700, 560), scale=5)
save("r_karaoke_neon", (1370, 380, 1470, 560), scale=8)
save("r_far_right_neon", (2250, 380, 2460, 640), scale=6)
save("r_left_posters2", (350, 350, 520, 520), scale=7)
save("r_price_cards", (1975, 300, 2460, 420), scale=5)
save("r_counter_top", (2000, 590, 2460, 760), scale=4)
