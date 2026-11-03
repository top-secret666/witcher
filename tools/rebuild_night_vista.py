"""Rebuild T_World_Vista as clean layers: night sky + night mountains + night-graded castle/path."""
from PIL import Image, ImageFilter
import numpy as np
from collections import deque
from pathlib import Path

raw = Path(r"C:\Users\DANA\.cursor\projects\d-HUH-witcher\assets")
sprites = Path(r"D:\HUH\witcher\Assets\Witcher\Art\Sprites")
res = Path(r"D:\HUH\witcher\Assets\Witcher\Resources\Pitch")
W, H = 1280, 720


def flood_black_alpha(rgb: np.ndarray, thr: float = 28.0) -> np.ndarray:
    """RGB uint8 -> RGBA with black/near-black flood-filled to alpha 0."""
    a = rgb.astype(np.float32)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    lum = (r + g + b) / 3
    alpha = np.where(lum < thr, 0, 255).astype(np.uint8)
    rgba = np.dstack([rgb, alpha])
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
        if lum[y, x] < thr + 8:
            rgba[y, x, 3] = 0
            q.extend([(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)])
    return rgba


def chroma_magenta(path: Path) -> np.ndarray:
    im = Image.open(path).convert("RGB").resize((W, H), Image.Resampling.LANCZOS)
    a = np.array(im)
    r, g, b = a[:, :, 0].astype(np.float32), a[:, :, 1].astype(np.float32), a[:, :, 2].astype(np.float32)
    s = 12
    corners = np.concatenate(
        [a[:s, :s].reshape(-1, 3), a[:s, -s:].reshape(-1, 3), a[-s:, :s].reshape(-1, 3), a[-s:, -s:].reshape(-1, 3)]
    )
    bg = np.median(corners, 0).astype(np.float32)
    diff = np.linalg.norm(a.astype(np.float32) - bg, axis=2)
    mag = ((np.minimum(r, b) - g) > 25) & (r > 120) & (b > 80) & (g < 160)
    black = (r < 22) & (g < 22) & (b < 22)
    mask = (diff < 80) | mag | black
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
        is_mag = (min(rr, bb) - gg) > 16 and rr > 100 and bb > 60 and gg < 165
        if rgba[y, x, 3] == 0 or dist < 90 or is_mag or (rr < 20 and gg < 20 and bb < 20):
            rgba[y, x] = 0
            q.extend([(y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)])
    return rgba


def main():
    # --- L0 clean night sky (NO moon) ---
    sky = Image.open(raw / "T_Sky_NightClean.jpg").convert("RGB").resize((W, H), Image.Resampling.LANCZOS)
    sky_a = np.array(sky).astype(np.float32)
    # crush any accidental bright orb (safety)
    sl = sky_a.mean(2)
    yy = np.linspace(0, 1, H)[:, None]
    xx = np.linspace(0, 1, W)[None, :]
    orb = (sl > 90) & (yy < 0.55) & (yy > 0.08) & (xx > 0.25) & (xx < 0.75)
    # keep stars: small bright points — erode large blobs only
    orb_u8 = (orb.astype(np.uint8) * 255)
    orb_img = Image.fromarray(orb_u8, "L").filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(7))
    om = (np.array(orb_img).astype(np.float32) / 255.0)
    om = np.array(Image.fromarray((om * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(3))).astype(np.float32) / 255
    for c in range(3):
        sky_a[:, :, c] = sky_a[:, :, c] * (1 - om) + np.array([6, 5, 14])[c] * om
    sky = Image.fromarray(np.clip(sky_a, 0, 255).astype(np.uint8))

    # --- L1 night mountains ---
    mtn_rgb = np.array(Image.open(raw / "T_Mountains_NightClean.jpg").convert("RGB").resize((W, H), Image.Resampling.LANCZOS))
    mtn = flood_black_alpha(mtn_rgb, thr=22)
    # keep only upper ridge band
    mtn[int(H * 0.52) :, :, 3] = 0
    # dim + cool
    rgb = mtn[:, :, 0:3].astype(np.float32)
    lum = rgb.mean(2)
    snow = lum > 85
    rgb[snow] = np.array([48.0, 58.0, 78.0])
    rgb *= np.array([0.72, 0.78, 0.92])
    mtn[:, :, 0:3] = np.clip(rgb, 0, 255)
    alpha = mtn[:, :, 3].astype(np.float32)
    alpha = np.array(Image.fromarray(alpha.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32)
    # slightly lower opacity so they sit behind haze
    mtn[:, :, 3] = np.clip(alpha * 0.92, 0, 255).astype(np.uint8)
    mtn_img = Image.fromarray(mtn, "RGBA")
    Image.fromarray(mtn, "RGBA").save(sprites / "T_Mountains_Night.png")

    base = Image.alpha_composite(sky.convert("RGBA"), mtn_img)

    # --- L2 castle / path / near forest from DAY (no sky, no far peaks) ---
    day = np.array(Image.open(sprites / "T_World_Vista_Day.png").convert("RGB").resize((W, H), Image.Resampling.LANCZOS)).astype(np.float32)
    r, g, b = day[:, :, 0], day[:, :, 1], day[:, :, 2]
    lum = (r + g + b) / 3
    sat = np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)

    # Day sky = warm/purple clouds
    day_sky = (yy < 0.48) & (
        ((r > 80) & (b > 60) & (g < 130) & (sat > 25))
        | ((r > 100) & (g > 40) & (b < 100) & (r > g))
        | ((lum > 70) & (sat > 35) & (yy < 0.35))
    )
    day_sky |= yy < 0.10

    # Day far mountains = bright snow / pale rock on sides
    far_peak = (yy < 0.45) & ((xx < 0.30) | (xx > 0.70)) & (
        ((lum > 100) & (sat < 70)) | ((lum > 70) & (sat < 45))
    )
    far_peak |= (yy < 0.38) & ((xx < 0.26) | (xx > 0.74))

    # Keep castle + hill + path + near trees
    castle = (yy > 0.14) & (yy < 0.56) & (xx > 0.30) & (xx < 0.70) & (sat < 50) & (lum > 28) & (lum < 175)
    keep = (~day_sky) & (~far_peak)
    keep |= castle
    keep |= yy > 0.46
    # never keep upper pure sky
    keep &= yy > 0.08

    ka = (keep.astype(np.float32) * 255.0)
    ka = np.array(Image.fromarray(ka.astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(2.0))).astype(np.float32)

    # Night grade FG: cool, darker, kill pink roofs a bit
    fg = day.copy()
    fg[:, :, 0] = fg[:, :, 0] * 0.62 + 8
    fg[:, :, 1] = fg[:, :, 1] * 0.70 + 10
    fg[:, :, 2] = np.minimum(255, fg[:, :, 2] * 0.78 + 22)
    # dim overall
    fg *= 0.92
    fg_rgba = np.dstack([np.clip(fg, 0, 255), np.clip(ka, 0, 255)]).astype(np.uint8)
    comp = Image.alpha_composite(base, Image.fromarray(fg_rgba, "RGBA"))

    # Soft cool haze near horizon (thin, no pink)
    ca = np.array(comp.convert("RGB")).astype(np.float32)
    mist = np.exp(-((yy - 0.30) ** 2) / (2 * 0.04**2)) * 0.22
    mist = mist * (1.0 - ((xx > 0.35) & (xx < 0.65) & (yy > 0.22) & (yy < 0.48)).astype(np.float32) * 0.5)
    haze = np.array([36.0, 40.0, 62.0])
    for c in range(3):
        ca[:, :, c] = ca[:, :, c] * (1 - mist) + haze[c] * mist

    out = np.clip(ca, 0, 255).astype(np.uint8)
    Image.fromarray(out).save(sprites / "T_World_Vista.png")
    Image.fromarray(out).save(res / "T_World_Vista.png")

    # Also replace broken night-sky resource so Unity sync can't reintroduce green moon
    sky.save(sprites / "T_Sky_NightMoon.png")
    sky.save(res / "T_Sky_NightMoon.png")
    print("vista rebuilt clean")

    # --- Near pines: large, sides only, no purple ---
    pine_path = raw / "T_L3_NearPines_Full.jpg"
    if not pine_path.exists():
        pine_path = raw / "T_L3_NearPines.jpg"
    pa = chroma_magenta(pine_path)
    r, g, b, a = pa[:, :, 0], pa[:, :, 1], pa[:, :, 2], pa[:, :, 3]
    purple = (a > 8) & (r > 45) & (b > 45) & (g < 95) & ((r > g + 10) | (b > g + 12))
    pa[purple, 0:3] = (16, 30, 20)
    pa[:, :, 0:3] = (pa[:, :, 0:3].astype(np.float32) * 0.75).clip(0, 255).astype(np.uint8)
    # clear center corridor
    pa[:, int(W * 0.34) : int(W * 0.66)] = 0
    # fill height (closer to player)
    pad = 4
    canvas = np.zeros((H, W, 4), np.uint8)
    pimg = Image.fromarray(pa, "RGBA").resize((W, H - 2 * pad), Image.Resampling.LANCZOS)
    canvas[pad : pad + pimg.size[1], :, :] = np.array(pimg)
    Image.fromarray(canvas, "RGBA").save(sprites / "T_L3_NearPines.png")
    Image.fromarray(canvas, "RGBA").save(res / "T_L3_NearPines.png")
    print("pines rebuilt")

    # preview (simulate near scale)
    v = Image.open(sprites / "T_World_Vista.png").convert("RGBA")
    s = Image.open(sprites / "T_L3_NearPines.png").convert("RGBA")
    ns = s.resize((int(W * 1.32), int(H * 1.32)), Image.Resampling.LANCZOS)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    layer.paste(ns, ((W - ns.width) // 2, H - ns.height + 36), ns)
    la = np.array(layer)
    la[:, int(W * 0.33) : int(W * 0.67)] = 0
    layer = Image.fromarray(la, "RGBA")
    c = Image.alpha_composite(v, layer)
    sw = Image.open(sprites / "T_Sword_Pixel.png").convert("RGBA")
    bb = sw.getbbox()
    if bb:
        sw = sw.crop(bb)
    tw, th = sw.size
    sc = min(0.32 * W / tw, 0.7 * H / th)
    sw = sw.resize((int(tw * sc), int(th * sc)), Image.Resampling.LANCZOS)
    sl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sl.paste(sw, (W - sw.width - 14, H - sw.height + 4), sw)
    Image.alpha_composite(c, sl).convert("RGB").save(sprites / "PREVIEW_Pitch_Composite.png")
    print("preview ok")


if __name__ == "__main__":
    main()
