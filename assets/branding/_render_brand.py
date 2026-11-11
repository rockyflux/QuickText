"""Render QuickText brand PNGs / ICO from tray-first mark geometry.

Small tray sizes (16/20/24/32) are drawn on an integer pixel grid — not
downscaled from the 256 art — so the shell icon stays sharp.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
MINT_HI = (0x57, 0xDF, 0xB8, 255)
MINT_LO = (0x22, 0x99, 0x7C, 255)
MINT = (0x3D, 0xC2, 0xA0, 255)
AMBER = (0xF2, 0xB4, 0x57, 255)
INK = (0x0F, 0x1D, 0x19, 255)
PAPER = (0xE7, 0xEF, 0xEC, 255)
SLATE = (0x8A, 0x9A, 0x96, 255)
SURFACE = (0x18, 0x25, 0x23, 255)
INK_BG = (0x0F, 0x17, 0x19, 255)


def lerp_rgb(a, b, t: float):
    t = 0.0 if t < 0 else (1.0 if t > 1 else t)
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def mint_gradient(size: int) -> Image.Image:
    seed = Image.new("RGBA", (2, 2))
    seed.putpixel((0, 0), MINT_HI)
    seed.putpixel((1, 0), lerp_rgb(MINT_HI, MINT_LO, 0.5))
    seed.putpixel((0, 1), lerp_rgb(MINT_HI, MINT_LO, 0.5))
    seed.putpixel((1, 1), MINT_LO)
    return seed.resize((size, size), Image.Resampling.BILINEAR)


def draw_mark_hires(size: int) -> Image.Image:
    """Vector-faithful mark for ≥48px / float ball / social."""
    scale = 4 if size <= 64 else (2 if size <= 128 else 1)
    big = size * scale
    bs = big / 64.0
    canvas = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    pad, rad = 5 * bs, 15 * bs
    grad = mint_gradient(big)
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (pad, pad, big - pad - 1, big - pad - 1), radius=rad, fill=255
    )
    tile = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    tile.paste(grad, (0, 0), mask)
    canvas = Image.alpha_composite(canvas, tile)

    d = ImageDraw.Draw(canvas)
    stroke = max(1, int(round(1.5 * bs)))
    inset = pad + 0.75 * bs
    d.rounded_rectangle(
        (inset, inset, big - inset - 1, big - inset - 1),
        radius=max(1, rad - 0.75 * bs),
        outline=(255, 255, 255, 56),
        width=stroke,
    )
    d.rounded_rectangle(
        (14 * bs, 26 * bs, 34 * bs, 39 * bs), radius=4 * bs, fill=AMBER
    )
    d.rounded_rectangle(
        (40 * bs, 16 * bs, 47 * bs, 48 * bs), radius=3.5 * bs, fill=INK
    )
    d.rounded_rectangle(
        (34 * bs, 16 * bs, 53 * bs, 21 * bs), radius=2.5 * bs, fill=INK
    )
    d.rounded_rectangle(
        (34 * bs, 43 * bs, 53 * bs, 48 * bs), radius=2.5 * bs, fill=INK
    )

    if scale > 1:
        return canvas.resize((size, size), Image.Resampling.LANCZOS)
    return canvas


def _fill_round_rect(px, size: int, x0, y0, x1, y1, color, radius: int = 0) -> None:
    """Integer-grid rounded rect (inclusive coords), no antialias — sharp tray pixels."""
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(size - 1, x1), min(size - 1, y1)
    if x1 < x0 or y1 < y0:
        return
    r = max(0, min(radius, (x1 - x0) // 2, (y1 - y0) // 2))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if r <= 0:
                px[x, y] = color
                continue
            # Corner distance checks
            if x < x0 + r and y < y0 + r:
                if (x - (x0 + r)) ** 2 + (y - (y0 + r)) ** 2 > r * r:
                    continue
            elif x > x1 - r and y < y0 + r:
                if (x - (x1 - r)) ** 2 + (y - (y0 + r)) ** 2 > r * r:
                    continue
            elif x < x0 + r and y > y1 - r:
                if (x - (x0 + r)) ** 2 + (y - (y1 - r)) ** 2 > r * r:
                    continue
            elif x > x1 - r and y > y1 - r:
                if (x - (x1 - r)) ** 2 + (y - (y1 - r)) ** 2 > r * r:
                    continue
            px[x, y] = color


def draw_mark_tray(size: int) -> Image.Image:
    """Pixel-grid mark for shell tray sizes. Reads as amber bar + I-beam at 16px."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()

    # Scale layout from a 16-cell reference so every size stays on integers.
    # 16-cell map (1 cell = size/16 px):
    #   tile inset 1..14, amber 3..7 x 6..9, stem 10..11 x 3..12, caps 9..12
    def c(n: float) -> int:
        return int(round(n * size / 16.0))

    # Solid mint tile (flat — gradient aliases badly at 16px)
    _fill_round_rect(px, size, c(1), c(1), c(15) - 1, c(15) - 1, MINT, radius=max(2, c(3)))

    # Amber text block
    _fill_round_rect(px, size, c(3), c(6), c(8) - 1, c(10) - 1, AMBER, radius=max(1, c(1)))

    # I-beam caret (ink)
    _fill_round_rect(px, size, c(10), c(3), c(12) - 1, c(13) - 1, INK, radius=max(1, c(1)))  # stem
    _fill_round_rect(px, size, c(9), c(3), c(13) - 1, c(4) - 1, INK, radius=max(0, c(1) - 1))  # top
    _fill_round_rect(px, size, c(9), c(12), c(13) - 1, c(13) - 1, INK, radius=max(0, c(1) - 1))  # bottom

    return img


def draw_mark(size: int) -> Image.Image:
    if size <= 32:
        return draw_mark_tray(size)
    return draw_mark_hires(size)


def save_ico(path: Path, sizes: list[int]) -> None:
    # BMP entries (not PNG-compressed): System.Drawing.Icon size-picking is reliable
    # with classic DIB frames; PNG-in-ICO often makes the tray downscale the 256 frame.
    images = [draw_mark(s) for s in sorted(sizes)]
    images[-1].save(
        path,
        format="ICO",
        bitmap_format="bmp",
        sizes=[(s, s) for s in sorted(sizes)],
        append_images=images[:-1],
    )


def try_font(size: int, bold: bool = True) -> ImageFont.ImageFont:
    candidates = [
        r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc",
        r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf",
    ]
    for c in candidates:
        try:
            return ImageFont.truetype(c, size)
        except OSError:
            continue
    return ImageFont.load_default()


def draw_social(width: int, height: int) -> Image.Image:
    base = Image.new("RGBA", (width, height), INK_BG)
    glow = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    cx, cy = int(width * 0.88), int(height * -0.05)
    for r, a in ((int(width * 0.55), 36), (int(width * 0.35), 28), (int(width * 0.2), 20)):
        gd.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(0x3D, 0xC2, 0xA0, a))
    img = Image.alpha_composite(base, glow)
    draw = ImageDraw.Draw(img)

    pad = int(width * 0.055)
    mark_size = int(height * 0.22)
    mark = draw_mark_hires(mark_size)
    img.paste(mark, (pad, pad), mark)

    title = try_font(int(height * 0.13), bold=True)
    sub = try_font(int(height * 0.055), bold=False)
    mono = try_font(int(height * 0.042), bold=False)

    tx = pad + mark_size + int(width * 0.02)
    ty = pad + int(mark_size * 0.12)
    draw.text((tx, ty), "QuickText", font=title, fill=PAPER)
    bbox = draw.textbbox((tx, ty), "QuickText", font=title)
    caret_h = int((bbox[3] - bbox[1]) * 0.86)
    caret_w = max(3, int(height * 0.012))
    cy0 = bbox[1] + int((bbox[3] - bbox[1] - caret_h) / 2)
    draw.rounded_rectangle(
        (bbox[2] + 4, cy0, bbox[2] + 4 + caret_w, cy0 + caret_h),
        radius=max(1, caret_w // 2),
        fill=MINT,
    )

    draw.text(
        (pad, pad + mark_size + int(height * 0.06)),
        "Your text, a keystroke away.",
        font=sub,
        fill=PAPER,
    )
    draw.text(
        (pad, pad + mark_size + int(height * 0.14)),
        "常用文本 · 拼音直呼 · 本地文件",
        font=mono,
        fill=SLATE,
    )

    panel_w, panel_h = int(width * 0.42), int(height * 0.62)
    px = width - pad - panel_w
    py = (height - panel_h) // 2
    draw.rounded_rectangle(
        (px, py, px + panel_w, py + panel_h),
        radius=int(height * 0.04),
        fill=SURFACE,
    )
    draw.text(
        (px + int(panel_w * 0.06), py + int(panel_h * 0.08)),
        "⌕  hf",
        font=try_font(int(height * 0.048), bold=False),
        fill=PAPER,
    )
    draw.rectangle(
        (
            px + int(panel_w * 0.22),
            py + int(panel_h * 0.09),
            px + int(panel_w * 0.22) + max(2, int(width * 0.002)),
            py + int(panel_h * 0.09) + int(height * 0.045),
        ),
        fill=MINT,
    )
    rows = [("回复模板", True), ("邮箱", False), ("签名", False)]
    ry = py + int(panel_h * 0.28)
    row_h = int(panel_h * 0.16)
    for name, on in rows:
        if on:
            draw.rounded_rectangle(
                (px + 8, ry, px + panel_w - 8, ry + row_h),
                radius=10,
                fill=(0x3D, 0xC2, 0xA0, 30),
            )
            draw.rounded_rectangle(
                (px + 12, ry + 8, px + 16, ry + row_h - 8),
                radius=2,
                fill=MINT,
            )
        draw.text(
            (px + int(panel_w * 0.08), ry + int(row_h * 0.28)),
            name,
            font=try_font(int(height * 0.042), bold=True),
            fill=PAPER,
        )
        ry += row_h + 6

    return img.convert("RGB")


def main() -> None:
    draw_mark_hires(256).save(ROOT / "quicktext-256.png")
    draw_mark_hires(128).save(ROOT / "quicktext-float.png")
    # 16/20/24/32 = tray DPI ladder; keep larger sizes for alt-tab / exe.
    save_ico(ROOT / "quicktext.ico", [16, 20, 24, 32, 40, 48, 64, 128, 256])
    draw_social(1200, 630).save(ROOT / "quicktext-social.png", quality=95)
    draw_social(2400, 1260).save(ROOT / "quicktext-social-2x.png", quality=95)
    print("ok")


if __name__ == "__main__":
    main()
