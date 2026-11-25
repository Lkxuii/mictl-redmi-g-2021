# 生成 MiCtl 应用图标（多尺寸 .ico）→ MiCtl/assets/app.ico
# 图标与托盘图标同一套视觉：深色圆角底 + 橙色 M。
# 用法: python MiCtl/assets/make_icon.py
import os
import sys

from PIL import Image, ImageDraw, ImageFont

BG = (27, 27, 34, 255)        # Theme.Card
BORDER = (44, 44, 55, 255)    # Theme.CardBorder
ACCENT = (255, 122, 47, 255)  # Theme.Accent

FONT_CANDIDATES = [
    "C:/Windows/Fonts/seguisb.ttf",   # Segoe UI Semibold
    "C:/Windows/Fonts/segoeui.ttf",
]


def draw_size(s):
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    r = max(2, int(s * 0.22))
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=BG,
                        outline=BORDER, width=max(1, s // 80))
    font = None
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            font = ImageFont.truetype(path, int(s * 0.64))
            break
    if font is None:
        font = ImageFont.load_default()
    text = "M"
    bbox = d.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    x = (s - tw) / 2 - bbox[0]
    y = (s - th) / 2 - bbox[1]
    d.text((x, y), text, font=font, fill=ACCENT)
    return im


def main():
    sizes = [256, 128, 64, 48, 32, 24, 16]
    images = [draw_size(s) for s in sizes]
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "app.ico")
    images[0].save(out, format="ICO", sizes=[(s, s) for s in sizes])
    print("saved", out, os.path.getsize(out), "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
