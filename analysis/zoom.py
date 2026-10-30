from PIL import Image
im = Image.open(r"D:\dsh file\StillPoint\screenshot-annotated.png")
im.crop((0, 1440, 1300, 1820)).save(r"D:\dsh file\StillPoint\analysis\legend_left.png")
im.crop((1260, 1440, 2560, 1820)).save(r"D:\dsh file\StillPoint\analysis\legend_right.png")
print("ok")
