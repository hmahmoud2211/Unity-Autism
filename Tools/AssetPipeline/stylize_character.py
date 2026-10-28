"""Repaints the Rocketbox boy (Male_Child_01, MIT) in NUMU's serious-game style (reference: a JeStiMulE
character): flat pale skin, a smooth orange hair cap, thick dark brows, light blue eyes, a dark sweater with
wide black/grey horizontal stripes and plain trousers. Photographic detail (pores, hair strands, denim, folds)
is smoothed away so the model reads as a soft, simply shaded game character.

Usage: python3 -I stylize_character.py <project_dir>
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

PROJECT = os.path.abspath(sys.argv[1])
SRC = os.path.join(PROJECT, "Assets/ThirdParty/Rocketbox/Avatars/Male_Child_01/Textures")
OUT = os.path.join(PROJECT, "Assets/NUMU/Characters/Stylized/Textures")

SKIN = np.array([240, 205, 178], np.float32)      # pale peach, as in the reference
HAIR = np.array([184, 98, 45], np.float32)        # burnt orange, as in the reference
BROW = np.array([46, 32, 26], np.float32)
IRIS = np.array([118, 166, 205], np.float32)      # light blue
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
    skin = skin_area & ~hair & (L > 0.2) & (r > g) & (g > b * 0.85)
    brow_band = face & (y > 0.215) & (y < 0.262)
    brows = brow_band & (L < 0.42)
    # Thick, dark brows that read from a game camera distance.
    brows = np.asarray(Image.fromarray((brows * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(19)).filter(ImageFilter.GaussianBlur(3)), np.float32) > 90
    brows &= face & (y > 0.208) & (y < 0.258)

    # Flat, lightly shaded skin: only broad form shading survives (no pores, no photographic lighting).
    out = soft_shade(a, skin, SKIN, 0.45, 10)
    # Hair: one smooth colour with only broad shading, like a simple modelled hair cap.
    out = soft_shade(out, hair, HAIR, 0.55, 26)
    k = feather(brows, 2)
    out = out * (1 - k) + BROW * k
    # A simple, soft mouth: the original lip area, flattened to one muted rose tone.
    lips = face & (y > 0.385) & (y < 0.418) & (np.abs(x - 0.5) < 0.045) & (r - g > 50) & (r - b > 55) & (L > 0.25)
    lips = np.asarray(Image.fromarray((lips * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)), np.float32)[..., None] / 255.0
    out = out * (1 - lips * 0.8) + np.array([196, 140, 128], np.float32) * lips * 0.8
    # Iris (eye texture block at the bottom): grey-green → light blue, whites kept bright.
    eye = (y > 0.82) & (x > 0.2) & (x < 0.36)
    iris = eye & (L < 0.55) & (L > 0.12) & (np.abs(r - g) < 40)
    out[iris] = (IRIS * (0.65 + 0.7 * L[iris][:, None]))
    whites = eye & (L > 0.62)
    out[whites] = np.minimum(255, out[whites] * 1.05 + 8)
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
