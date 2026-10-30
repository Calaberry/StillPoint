from PIL import Image, ImageDraw, ImageFont
import os
fonts = ["malgun.ttf","msyh.ttc","gulim.ttc","batang.ttc","simsun.ttc","NotoSansCJK-Regular.ttc","arialuni.ttf"]
out = Image.new("RGB",(900,420),(20,20,20))
d = ImageDraw.Draw(out)
y=10
for f in fonts:
    p = os.path.join(r"C:\Windows\Fonts", f)
    if not os.path.exists(p):
        d.text((10,y), f+" MISSING", fill=(255,80,80)); y+=40; continue
    try:
        ft = ImageFont.truetype(p, 30)
        d.text((10,y), f+": 바다의 피드 신선한 해산물 邓恩 热狗", font=ft, fill=(255,255,255))
    except Exception as e:
        d.text((10,y), f+" ERR "+str(e)[:40], fill=(255,80,80))
    y+=40
out.save(r"D:\dsh file\StillPoint\analysis\fonttest.png")
print("ok")
