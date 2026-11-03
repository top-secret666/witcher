from PIL import Image, ImageFilter
import numpy as np
from collections import deque
from pathlib import Path

raw = Path(r"C:\Users\DANA\.cursor\projects\d-HUH-witcher\assets")
sprites = Path(r"D:\HUH\witcher\Assets\Witcher\Art\Sprites")
res = Path(r"D:\HUH\witcher\Assets\Witcher\Resources\Pitch")
W, H = 1280, 720


def chroma(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGB").resize((W, H), Image.Resampling.LANCZOS)
    a = np.array(im)
    r, g, b = a[:, :, 0].astype(np.float32), a[:, :, 1].astype(np.float32), a[:, :, 2].astype(np.float32)
    s = 15
    corners = np.concatenate(
        [
            a[:s, :s].reshape(-1, 3),
            a[:s, -s:].reshape(-1, 3),
            a[-s:, :s].reshape(-1, 3),
            a[-s:, -s:].reshape(-1, 3),
        ]
    )
    bg = np.median(corners, 0).astype(np.float32)
    diff = np.linalg.norm(a.astype(np.float32) - bg, axis=2)
    mag = ((np.minimum(r, b) - g) > 28) & (r > 130) & (b > 90) & (g < 160)
    mask = (diff < 85) | mag | ((diff < 125) & ((np.minimum(r, b) - g) > 12))
    rgba = np.dstack([a, np.where(mask, 0, 255).astype(np.uint8)])
    vis = np.zeros((H, W), bool)
    q = deque()
    for x in range(W):
        q.append((0, x))
        q.append((H - 1, x))
    for y in range(H):
        q.append((y, 0))
        q.append((y, W - 1))
    while q:
        y, x = q.popleft()
        if not (0 <= y < H and 0 <= x < W) or vis[y, x]:
            continue
        vis[y, x] = True
        rr, gg, bb = map(int, rgba[y, x, :3])
        dist = abs(rr - bg[0]) + abs(gg - bg[1]) + abs(bb - bg[2])
        is_mag = (min(rr, bb) - gg) > 18 and rr > 110 and bb > 70 and gg < 170
        if rgba[y, x, 3] == 0 or dist < 95 or is_mag:
            rgba[y, x] = 0
            q.extend([(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)])
    return Image.fromarray(rgba, "RGBA")


def main():
    mtn = chroma(raw / "T_Mountains_Fill.jpg")
    ma = np.array(mtn)
    ma[int(H * 0.58) :, :, 3] = 0

    # Close small holes in mountain alpha
    alpha = ma[:, :, 3].astype(np.uint8)
    solid = alpha > 40
    pad = np.pad(solid.astype(np.uint8), 1)
    for _ in range(4):
        n = pad[:-2, 1:-1] + pad[2:, 1:-1] + pad[1:-1, :-2] + pad[1:-1, 2:]
        fill = (~solid) & (n >= 3)
        solid |= fill
        alpha[fill] = 230
        ma[fill, 0] = 70
        ma[fill, 1] = 55
        ma[fill, 2] = 90
        pad = np.pad(solid.astype(np.uint8), 1)

    alpha = np.array(Image.fromarray(alpha, "L").filter(ImageFilter.GaussianBlur(2.0)))
    ma[:, :, 3] = alpha
    ma[:, :, 0] = (ma[:, :, 0].astype(np.float32) * 0.88).clip(0, 255)
    ma[:, :, 1] = (ma[:, :, 1].astype(np.float32) * 0.92).clip(0, 255)
    ma[:, :, 2] = np.minimum(255, ma[:, :, 2].astype(np.float32) * 1.06)
    mtn2 = Image.fromarray(ma.astype(np.uint8), "RGBA")

    night = Image.open(raw / "T_Sky_NightMoon.jpg").convert("RGB").resize((W, H), Image.Resampling.LANCZOS)
    day = Image.open(sprites / "T_World_Vista_Day.png").convert("RGB").resize((W, H), Image.Resampling.LANCZOS)

    base = Image.alpha_composite(night.convert("RGBA"), mtn2)

    da = np.array(day).astype(np.float32)
    r, g, b = da[:, :, 0], da[:, :, 1], da[:, :, 2]
    lum = (r + g + b) / 3
    sat = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)
    yy = np.linspace(0, 1, H)[:, None]
    xx = np.linspace(0, 1, W)[None, :]

    orange = (r > 90) & (g > 35) & (b < 120) & (r > g + 10)
    purple = (b > 55) & (r > 35) & (g < 110)
    sky = (yy < 0.48) & (orange | purple | ((lum > 80) & (sat > 28) & (g < r + 15)))
    sky |= yy < 0.1

    # Drop day far peaks (they had holes); keep castle / path / near forest
    is_far_peak = (yy < 0.40) & (sat < 55) & (lum > 90) & ((xx < 0.30) | (xx > 0.70))
    castle = (yy > 0.15) & (yy < 0.55) & (xx > 0.32) & (xx < 0.68) & (sat < 45) & (lum > 35) & (lum < 165)
    fg_keep = (~sky) & (~is_far_peak)
    fg_keep |= castle | (yy > 0.45)

    fg_alpha = (fg_keep.astype(np.float32) * 255).astype(np.uint8)
    fg_alpha = np.array(Image.fromarray(fg_alpha, "L").filter(ImageFilter.GaussianBlur(1.5)))
    fga = da.copy()
    fga[:, :, 0] *= 0.94
    fga[:, :, 1] *= 0.97
    fga[:, :, 2] *= 1.03
    fga += 5
    fg = np.dstack([np.clip(fga, 0, 255).astype(np.uint8), fg_alpha])
    comp = Image.alpha_composite(base, Image.fromarray(fg, "RGBA"))

    # Soft continuous mist over dark sky only
    ca = np.array(comp.convert("RGBA")).astype(np.float32)
    clum = ca[:, :, 0:3].mean(axis=2)
    skyish = (clum < 85) & (yy[:, 0][:, None] < 0.42)
    mist = np.zeros((H, W), np.float32)
    yc = 0.27
    xs = np.sin(np.pi * np.linspace(0, 1, W)) ** 0.6
    for y in range(H):
        mist[y, :] = np.exp(-((y / H - yc) ** 2) / (2 * 0.05**2)) * (0.55 + 0.45 * xs)
    hm = mist * skyish.astype(np.float32) * 0.38
    haze_col = np.array([88.0, 48.0, 108.0])
    for c in range(3):
        ca[:, :, c] = ca[:, :, c] * (1 - hm) + haze_col[c] * hm
    out = np.clip(ca[:, :, 0:3], 0, 255).astype(np.uint8)
    Image.fromarray(out).save(sprites / "T_World_Vista.png")
    Image.fromarray(out).save(res / "T_World_Vista.png")
    print("vista mountains continuous")

    # Full pines with padding so tips/bases aren't cropped
    pines = chroma(raw / "T_L3_NearPines_Full.jpg")
    pa = np.array(pines)
    r, g, b, a = pa[:, :, 0], pa[:, :, 1], pa[:, :, 2], pa[:, :, 3]
    purple = (a > 10) & (r > 55) & (b > 55) & (g < 100) & ((r > g + 15) | (b > g + 20))
    pa[purple, 0:3] = (24, 42, 28)
    pa[:, :, 0:3] = (pa[:, :, 0:3].astype(np.float32) * 0.82).clip(0, 255).astype(np.uint8)
    cx0, cx1 = int(W * 0.34), int(W * 0.66)
    pa[:, cx0:cx1] = 0
    pad = 36
    canvas = np.zeros((H, W, 4), np.uint8)
    pimg = Image.fromarray(pa, "RGBA").resize((W, H - 2 * pad), Image.Resampling.LANCZOS)
    canvas[pad : pad + pimg.size[1], :, :] = np.array(pimg)
    Image.fromarray(canvas, "RGBA").save(sprites / "T_L3_NearPines.png")
    Image.fromarray(canvas, "RGBA").save(res / "T_L3_NearPines.png")
    print("pines padded")

    v = Image.open(sprites / "T_World_Vista.png").convert("RGBA")
    s = Image.open(sprites / "T_L3_NearPines.png").convert("RGBA")
    c = Image.alpha_composite(v, s)
    sw = Image.open(sprites / "T_Sword_Pixel.png").convert("RGBA")
    bb = sw.getbbox()
    if bb:
        sw = sw.crop(bb)
    tw, th = sw.size
    sc = min(0.3 * W / tw, 0.65 * H / th)
    sw = sw.resize((int(tw * sc), int(th * sc)), Image.Resampling.LANCZOS)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    layer.paste(sw, (W - sw.width - 16, H - sw.height + 6), sw)
    Image.alpha_composite(c, layer).convert("RGB").save(sprites / "PREVIEW_Pitch_Composite.png")
    print("preview ok")


if __name__ == "__main__":
    main()
