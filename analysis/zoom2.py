from PIL import Image, ImageEnhance, ImageFilter
img = Image.open(r"D:\dsh file\StillPoint\analysis\source.jpg").convert("RGB")
c = img.crop((560, 0, 1280, 230)).resize((2160, 690), Image.LANCZOS)
c = ImageEnhance.Contrast(c).enhance(1.2)
c = c.filter(ImageFilter.UnsharpMask(radius=2, percent=150, threshold=3))
c.save(r"D:\dsh file\StillPoint\analysis\crops6\t_red_tail.png")
print("ok", c.size)
