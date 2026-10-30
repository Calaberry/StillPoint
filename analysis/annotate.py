"""Annotated figure with per-character font fallback (Hangul -> Malgun, CJK -> YaHei)."""
from PIL import Image, ImageDraw, ImageFont

SRC = r"D:\dsh file\StillPoint\analysis\source.jpg"
OUT = r"D:\dsh file\StillPoint\screenshot-annotated.png"

CJK = "C:/Windows/Fonts/msyh.ttc"
KOR = "C:/Windows/Fonts/malgun.ttf"
_cache = {}


def font_for(ch, size):
    hangul = ("\u1100" <= ch <= "\u11ff") or ("\uac00" <= ch <= "\ud7a3")
    path = KOR if hangul else CJK
    key = (path, size)
    if key not in _cache:
        _cache[key] = ImageFont.truetype(path, size)
    return _cache[key]


def text_len(d, s, size):
    return sum(font_for(c, size).getlength(c) for c in s)


def draw_rich(d, xy, s, size, fill):
    x, y = xy
    for c in s:
        f = font_for(c, size)
        d.text((x, y), c, font=f, fill=fill)
        x += f.getlength(c)
    return x


img = Image.open(SRC).convert("RGB")
W, H = img.size
STRIP = 380
canvas = Image.new("RGB", (W, H + STRIP), (14, 14, 16))
canvas.paste(img, (0, 0))
d = ImageDraw.Draw(canvas)

ACCENT = (0, 230, 255)
callouts = [
    (1,  (1300, 645, 1510, 742), "Pvt. Cho 队友名牌（游戏内 UI）"),
    (2,  (860, 1110, 1900, 1202), "邓恩的中文台词字幕（简中本地化）"),
    (3,  (1050, 0, 1690, 44), "MSI Afterburner / RTSS 性能浮层"),
    (4,  (1625, 180, 1800, 350), "黄色小鸡贴纸（视频后期叠加）"),
    (5,  (890, 150, 1055, 330), "紫色章鱼贴纸（视频后期叠加）"),
    (6,  (345, 20, 1040, 200), "红字灯箱「바다의 피드」"),
    (7,  (205, 300, 735, 600), "海鲜店「신선한 해산물」+ 联系电话"),
    (8,  (1985, 315, 2445, 525), "三语菜单卡（韩/英/中）"),
    (9,  (1990, 545, 2460, 1270), "热狗摊与「닭강정 하우스」"),
    (10, (1285, 60, 1420, 245), "黄色灯箱「아귀찜」"),
    (11, (1280, 270, 1480, 385), "MONEY EXCHANGE（换钱所）"),
]

for n, (l, t, r, b), _ in callouts:
    d.rectangle([l, t, r, b], outline=ACCENT, width=5)
    tw = text_len(d, str(n), 30)
    cx, cy = (l + 8, b + 8) if t < 60 else (l + 8, t + 8)
    d.rounded_rectangle([cx, cy, cx + tw + 26, cy + 46], radius=8, fill=(0, 0, 0))
    draw_rich(d, (cx + 13, cy + 2), str(n), 30, ACCENT)

pad = 30
draw_rich(d, (pad, H + 18), "图注：Modern Warfare 4「Mayhem」开场夜巡 · 2560×1440 截图", 32, (245, 245, 245))
col_w = (W - 2 * pad) // 2
for i, (n, _, text) in enumerate(callouts):
    col, row = divmod(i, 6)
    x = pad + col * col_w
    y = H + 80 + row * 48
    d.ellipse([x, y + 4, x + 32, y + 36], outline=ACCENT, width=3, fill=(0, 0, 0))
    tw = text_len(d, str(n), 29)
    draw_rich(d, (x + 16 - tw / 2, y - 2), str(n), 29, ACCENT)
    draw_rich(d, (x + 48, y), text, 29, (228, 228, 228))

canvas.save(OUT)
print("saved", OUT, canvas.size)
prev = canvas.resize((1600, int(canvas.height * 1600 / canvas.width)), Image.LANCZOS)
prev.save(r"D:\dsh file\StillPoint\analysis\annotated_preview.png")
print("preview", prev.size)
