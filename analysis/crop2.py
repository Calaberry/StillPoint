"""Second pass: wider / corrected crops for signage reading."""
from PIL import Image, ImageEnhance
import os

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\analysis\crops2"
os.makedirs(OUT, exist_ok=True)

img = Image.open(SRC).convert("RGB")

regions = {
    "a_red_sign_full":   (320,   0, 1120, 200, 3),
    "b_seafood_full":    (190, 290,  500, 460, 5),
    "c_white_vertical":  (1540, 110, 1780, 320, 4),
    "d_topright_white":  (1790,  10, 2080, 220, 4),
    "e_menu_board":      (1970, 295, 2460, 545, 4),
    "f_menu_sign":       (1080, 400, 1270, 500, 5),
    "g_yellow_vert":     (1260,  50, 1440, 250, 5),
    "h_center_wall":     (1270, 270, 1470, 500, 4),
    "i_left_shop":       (360, 370,  650, 540, 4),
    "j_left_posters":    (360, 350,  520, 500, 5),
    "k_right_red_menu":  (2080, 780, 2470, 960, 4),
    "l_stall_counter":   (1980, 560, 2460, 820, 3),
    "m_bottom_right":    (2080, 1150, 2470, 1330, 4),
    "n_left_edge":       (0,    10,  120, 260, 5),
    "o_mid_sign":        (1130, 200, 1300, 420, 4),
    "p_shop_door":       (400, 300,  720, 620, 3),
}

for name, (l, t, r, b, scale) in regions.items():
    crop = img.crop((l, t, r, b))
    big = crop.resize((crop.width * scale, crop.height * scale), Image.LANCZOS)
    big = ImageEnhance.Contrast(big).enhance(1.25)
    big = ImageEnhance.Brightness(big).enhance(1.15)
    big = ImageEnhance.Sharpness(big).enhance(1.8)
    big.save(os.path.join(OUT, name + ".png"))
    print(name, crop.size, "->", big.size)
