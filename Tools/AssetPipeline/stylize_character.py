"""Repaints the Rocketbox boy (Male_Child_01, MIT) in NUMU's serious-game style (reference: a JeStiMulE
character): flat pale skin, a smooth orange hair cap, thick dark brows, light blue eyes, a dark sweater with
wide black/grey horizontal stripes and plain trousers. Photographic detail (pores, hair strands, denim, folds)
is smoothed away so the model reads as a soft, simply shaded game character.

Usage: python3 -I stylize_character.py <project_dir>
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

PROJECT = os.path.abspath(sys.argv[1])
SRC = os.path.join(PROJECT, "Assets/ThirdParty/Rocketbox/Avatars/Male_Child_01/Textures")
OUT = os.path.join(PROJECT, "Assets/NUMU/Characters/Stylized/Textures")

SKIN = np.array([240, 205, 178], np.float32)      # pale peach, as in the reference
HAIR = np.array([184, 98, 45], np.float32)        # burnt orange, as in the reference
BROW = np.array([46, 32, 26], np.float32)
LASH = np.array([38, 26, 22], np.float32)
LIPS = np.array([200, 142, 132], np.float32)      # muted rose
SCLERA = np.array([248, 246, 242], np.float32)
IRIS_OUT = np.array([62, 104, 150], np.float32)   # light blue iris: darker at the rim, lighter near the pupil
IRIS_IN = np.array([142, 186, 220], np.float32)
LIMBUS = np.array([30, 44, 62], np.float32)
PUPIL = np.array([14, 14, 18], np.float32)
STRIPE_DARK = np.array([30, 30, 33], np.float32)
STRIPE_GREY = np.array([78, 78, 82], np.float32)
PANTS = np.array([58, 62, 74], np.float32)


def smooth(img, radius):
    """Edge-preserving-ish flattening: median then a light blur."""
    return img.filter(ImageFilter.MedianFilter(radius)).filter(ImageFilter.GaussianBlur(radius / 3))


def lum(a):
    return (a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114) / 255.0


def soft_shade(a, mask, color, strength, blur):
    """Replaces masked pixels by a flat colour, keeping only broad, softened light/dark variation."""
    l = lum(np.asarray(Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur)), np.float32))
    mean = l[mask].mean() if mask.any() else 0.5
    shade = np.clip(1.0 + (l - mean) * strength, 0.75, 1.15)[..., None]
    out = a.copy()
    out[mask] = (color * shade)[mask]
    return out


def feather(mask, px):
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(px))
    return np.asarray(m, np.float32)[..., None] / 255.0


def stroke(H, W, pts, widths, samples=64):
    """Mask of a tapered brush stroke along a quadratic/cubic Bezier through control points (pixels)."""
    pts = np.array(pts, np.float64)
    t = np.linspace(0, 1, samples)[:, None]
    n = len(pts) - 1
    from math import comb
    curve = sum(comb(n, i) * (1 - t) ** (n - i) * t ** i * pts[i] for i in range(n + 1))
    w = np.interp(t[:, 0], np.linspace(0, 1, len(widths)), widths)
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    for (cx, cy), r in zip(curve, w / 2):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return np.asarray(img) > 127


def paint(out, mask, color, blur, alpha=1.0):
    k = feather(mask, blur) * alpha
    return out * (1 - k) + color * k


def eyeballs(out, W, H):
    """Clean stylised eyes: bright whites, a large round light-blue iris with a dark rim, a black pupil and a
    small highlight. Both eyeballs share one UV island centred on the iris; the island's radius maps almost
    linearly to the angle from the eye's axis (35 px = 20 deg, 70 = 39, 100 = 59, 117 = 79 at 2048 px)."""
    s = W / 2048.0
    cx, cy = 0.2632 * W, (1 - 0.0676) * H
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.hypot(xx - cx, yy - cy) / s
    island = d < 124
    iris_r, pupil_r = 47.0, 16.0  # ~27 deg and ~9 deg: a larger iris than the original (20 deg)
    t = np.clip((d - pupil_r) / (iris_r - pupil_r), 0, 1)[..., None]
    col = IRIS_IN * (1 - t) + IRIS_OUT * t
    # Dark limbal ring at the edge of the iris, soft pupil edge.
    rim = np.clip((d - (iris_r - 7)) / 4, 0, 1)[..., None]
    col = col * (1 - rim) + LIMBUS * rim
    pupil = np.clip((pupil_r + 1.5 - d) / 3, 0, 1)[..., None]
    col = col * (1 - pupil) + PUPIL * pupil
    # Whites outside the iris, fading to a soft shadow toward the back of the eyeball (hidden corners stay dim).
    white = np.clip((d - iris_r) / 2.5, 0, 1)[..., None]
    back = np.clip((d - 88) / 30, 0, 1)[..., None]
    sclera = SCLERA * (1 - back) + np.array([196, 170, 168], np.float32) * back
    col = col * (1 - white) + sclera * white
    # Highlight, up and toward the nose side.
    hl = np.clip((5.5 - np.hypot(xx - (cx - 11 * s), yy - (cy - 13 * s)) / s) / 2, 0, 1)[..., None]
    col = col * (1 - hl) + np.array([255, 255, 255], np.float32) * hl
    out[island] = col[island]


def head():
    src = Image.open(os.path.join(SRC, "cm001_head_color.png")).convert("RGB")
    W, H = src.size
    a = np.asarray(smooth(src, 7), np.float32)
    raw = np.asarray(src, np.float32)
    y, x = np.mgrid[0:H, 0:W] / np.array([H, W], np.float32)[:, None, None]
    L = lum(raw)
    r, g, b = raw[..., 0], raw[..., 1], raw[..., 2]
    upper = y < 0.6
    # Skin continues below the hair block in the centre (neck and shoulders); the bottom-left holds the eye/mouth textures.
    skin_area = (y < 0.75) & ~((x < 0.37) & (y > 0.58) & (L < 0.35))
    face = ((x - 0.5) / 0.175) ** 2 + ((y - 0.37) / 0.165) ** 2 < 1.0
    hair = upper & (L < 0.36) & ~face & (r > b)
    # A clean, rounded hairline (a modelled hair cap, not strands): grow, blur and re-threshold the mask.
    m = Image.fromarray((hair * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(5))
    m = m.filter(ImageFilter.GaussianBlur(10))
    hair = (np.asarray(m, np.float32) > 110) & upper
    # A side-swept fringe, lower on the forehead than the original hairline (as in the references): its lower edge
    # runs from just above the right brow to higher over the left temple.
    fringe_y = np.interp(x, [0.33, 0.40, 0.50, 0.60, 0.67], [0.186, 0.204, 0.200, 0.192, 0.180])
    hair |= (np.abs(x - 0.5) < 0.17) & (y < fringe_y) & (y > 0.12)
    # Keep the hair cap itself; small dark islands (ear canals) are skin, not hair.
    lab, n = ndi.label(hair)
    sizes = ndi.sum(hair, lab, range(1, n + 1))
    hair = np.isin(lab, 1 + np.nonzero(sizes > 0.01 * W * H)[0])
    skin = skin_area & ~hair & (L > 0.2) & (r > g) & (g > b * 0.85)
    # Below the face (neck and nape) everything that isn't hair is skin: no stubble or pore speckles left.
    skin |= skin_area & ~hair & (y > 0.45) & ~((x < 0.37) & (y > 0.58))
    # Flat, lightly shaded skin: only broad form shading survives (no pores, no photographic lighting).
    out = soft_shade(a, skin, SKIN, 0.45, 10)
    # Hair: one smooth colour with only broad shading, like a simple modelled hair cap.
    out = soft_shade(out, hair, HAIR, 0.55, 26)
    s = W / 2048.0
    # Thick, dark, gently arched brows (the reference's strongest facial feature), drawn as tapered strokes
    # over the original brow positions so they move with the brow blendshapes.
    brows = np.zeros((H, W), bool)
    for side in (-1, 1):
        def px(u, v):  # texture point mirrored to this side of the face (the UV layout is symmetric)
            return (W / 2 + side * (W / 2 - u * s), v * s)
        # outer end, arch peak, inner end (from the original brows); thickness tapers toward the outer end
        brows |= stroke(H, W, [px(788, 548), px(850, 509), px(968, 521)], [9 * s, 29 * s, 31 * s])
    out = paint(out, brows, BROW, 1.5)
    # A bold upper lash line hugging the top of each eye opening (the dark lid slit of the original texture),
    # thickest toward the outer corner.
    slit = (L < 0.28) & (y > 0.26) & (y < 0.31) & (np.abs(x - 0.5) > 0.03) & (np.abs(x - 0.5) < 0.15)
    lab, n = ndi.label(slit)
    sizes = ndi.sum(slit, lab, range(1, n + 1))
    slit = np.isin(lab, 1 + np.argsort(sizes)[-2:])  # the two eye openings, not stray lash pixels
    lash = np.zeros((H, W), bool)
    cols = np.nonzero(slit.any(axis=0))[0]
    top = np.array([np.argmax(slit[:, c]) for c in cols], np.float32)
    top = ndi.median_filter(top, 9)
    for side in (cols < W / 2, cols >= W / 2):
        cs, ts = cols[side], top[side]
        if len(cs) == 0:
            continue
        inner_first = abs(cs[0] - W / 2) < abs(cs[-1] - W / 2)
        f = np.linspace(0, 1, len(cs)) if inner_first else np.linspace(1, 0, len(cs))  # 0 inner corner, 1 outer
        thick = (3 + 8 * f ** 0.7) * s
        for c, t0, th in zip(cs, ts, thick):
            lash[int(t0 - th):int(t0 + 2 * s), c] = True
    out = paint(out, lash, LASH, 1.0)
    # A simple, soft mouth: the original lip shape (redder than the surrounding skin) in a muted rose.
    red = raw[..., 0] - raw[..., 1]
    red_bg = np.asarray(Image.fromarray(np.clip(red, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(30 * s)), np.float32)
    lips = (red - red_bg > 12) & (np.abs(x - 0.5) < 0.06) & (y > 0.38) & (y < 0.425)
    lips = ndi.binary_opening(lips, iterations=2)
    out = paint(out, lips, LIPS, 3 * s, 0.75)
    eyeballs(out, W, H)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "numu_boy_head.png"), optimize=True)


def body():
    src = Image.open(os.path.join(SRC, "cm001_body_color.png")).convert("RGB")
    W, H = src.size
    raw = np.asarray(src, np.float32)
    a = np.asarray(smooth(src, 7), np.float32)
    y, x = np.mgrid[0:H, 0:W] / np.array([H, W], np.float32)[:, None, None]
    r, g, b = raw[..., 0], raw[..., 1], raw[..., 2]
    L = lum(raw)
    bg = L < 0.03
    blue = (b > r + 25) & (b > g)
    torso = (x > 0.32) & (x < 0.68) & (y > 0.015) & (y < 0.85) & ~bg & (blue | ((y > 0.5) & (y < 0.75)))
    # Sleeves: only the blue fabric (the shoe and hand unwraps overlap these rectangles).
    sleeves = ((x < 0.32) | (x > 0.68)) & (y > 0.44) & (y < 0.86) & blue
    jeans = ((x < 0.32) | (x > 0.68)) & (y < 0.44) & ~bg
    hands = (y > 0.84) & ((x < 0.24) | (x > 0.76)) & ~bg & (r > b)

    out = a.copy()
    # Wide horizontal stripes: the torso unwrap is mirrored about the shoulders (y ≈ 0.48), the sleeve
    # unwraps run along the arm, so bands in V wrap around the body and the arms.
    band_t = np.abs(y - 0.48) * H / 34.0
    band_s = (y - 0.44) * H / 30.0
    stripes = np.where(torso, band_t, band_s)
    dark = (np.floor(stripes) % 2) == 0
    cloth = torso | sleeves
    shade = np.clip(1.0 + (lum(np.asarray(Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.GaussianBlur(5)), np.float32)) - 0.38) * 0.8, 0.8, 1.15)[..., None]
    col = np.where(dark[..., None], STRIPE_DARK, STRIPE_GREY) * shade
    out[cloth] = col[cloth]
    # V-neck on the front (the half of the torso unwrap below the neck hole, where the original print was):
    # skin inside the V, with a dark grey trim along its edges.
    d = np.clip((y - 0.47) / 0.13, 0, 1)
    vhalf = 0.075 * (1 - d)
    inside = (y > 0.47) & (y < 0.6) & (np.abs(x - 0.5) < vhalf) & torso
    trim = (y > 0.465) & (y < 0.615) & (np.abs(x - 0.5) < vhalf + 0.012) & torso & ~inside
    out[inside] = SKIN * 0.97
    out[trim] = STRIPE_DARK * 1.4
    out = soft_shade(out, jeans, PANTS, 0.7, 8)
    out = soft_shade(out, hands, SKIN, 0.8, 4)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "numu_boy_body.png"), optimize=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    head()
    body()
    print("stylized textures written to", OUT)
