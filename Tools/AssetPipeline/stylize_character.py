"""Restyles NUMU's human cast (Microsoft Rocketbox avatars, MIT) into one consistent serious-game look, after the main
style reference (Assets/References/Character_Expression_Reference.png, a JeStiMulE boy):

- flat, softly shaded skin (each character keeps their own skin tone);
- large clear eyes: bright whites, a big round iris with a dark rim, a black pupil, a highlight, a bold upper lash line;
- thick, gently arched brows drawn with one template relative to the eyes, so every face has the same construction;
- a simple soft mouth (the original lip shape in a muted tone);
- hair, headscarves and clothes flattened into a few clean colours with only broad shading (no strands, prints or
  fabric noise); hair cards keep their alpha.

Feature positions come from Tools/AssetPipeline/features/<Avatar>.json, measured from each facial rig in Unity
(NumuCastFeatures). The chosen colours are written back next to them (<Name>_style.json) for the Unity side (hair shell).

Usage: python3 -I stylize_character.py <project_dir> [name ...]
"""
import json
import os
import sys
from math import comb

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

PROJECT = os.path.abspath(sys.argv[1])
AVATARS = os.path.join(PROJECT, "Assets/ThirdParty/Rocketbox/Avatars")
FEATURES = os.path.join(PROJECT, "Tools/AssetPipeline/features")
OUT = os.path.join(PROJECT, "Assets/NUMU/Characters/Stylized/Textures")

# The cast. Only colours vary; the face construction is shared. Keys are output names; "source" is the Rocketbox avatar.
CAST = {
    # The player, after the main reference: warm ginger hair, dark brown eyes, a green sweater with tonal stripes.
    # Colours sampled from the reference (lit side).
    "NUMU_Boy": dict(source="Male_Child_01", skin=(228, 197, 164), hair=(186, 124, 66), brow=(78, 58, 36),
                     iris=(56, 36, 26), iris_r=54, fringe=True, body="sweater",
                     sweater=((92, 124, 58), (80, 108, 50)), pants=(72, 78, 92)),
    "Male_Child_01": dict(iris=(96, 136, 176)),
    "Male_Child_02": dict(iris=(64, 42, 30)),                                  # Omar
    "Female_Child_01": dict(iris=(96, 136, 168), hair=(206, 170, 112), brow=(122, 90, 58), hair_dE=9),  # Mia
    "Female_Child_02": dict(iris=(72, 48, 34), brow=(58, 38, 26), scarf=True),                # Lina
    "Female_Adult_01": dict(iris=(92, 126, 156), hair=(200, 162, 108), brow=(118, 88, 60), hair_dE=9),  # shopper
    "Female_Adult_06": dict(iris=(72, 48, 34), brow=(58, 38, 26), scarf=True),                # Mum
    "Female_Adult_12": dict(iris=(80, 56, 38)),                                # cashier Nora
    "Female_Adult_14": dict(iris=(92, 112, 78)),                               # teacher Ms. Sara
    "Male_Adult_02": dict(iris=(76, 54, 38)),                                  # shop assistant / park helper
    "Male_Adult_08": dict(iris=(88, 118, 148)),                                # Dad
    "Male_Adult_13": dict(iris=(78, 58, 40), cloth_merge=48),                 # Mr. Karim (plain sweater, no argyle)
}

SCLERA = np.array([248, 246, 242], np.float32)
PUPIL = np.array([14, 14, 18], np.float32)
LASH = np.array([38, 26, 22], np.float32)
REF_SKIN = np.array([225, 195, 161], np.float32)                 # the reference's lit cheek
REF_SKIN_HUE = REF_SKIN / REF_SKIN.mean()
# Eyeball UV island shared by all Rocketbox avatars: centre and scale (35 px = 20 deg at 2048 px).
IRIS_UV = (0.2632, 0.0676)


# ------------------------------------------------------------------ image helpers

def smooth(img, radius):
    """Edge-preserving-ish flattening: median then a light blur."""
    return img.filter(ImageFilter.MedianFilter(radius)).filter(ImageFilter.GaussianBlur(radius / 3))


def lum(a):
    return (a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114) / 255.0


def blur_lum(a, radius):
    return lum(np.asarray(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius)), np.float32))


def soft_shade(a, mask, color, strength, blur):
    """Replaces masked pixels by a flat colour, keeping only broad, softened light/dark variation."""
    l = blur_lum(a, blur)
    mean = l[mask].mean() if mask.any() else 0.5
    shade = np.clip(1.0 + (l - mean) * strength, 0.75, 1.15)[..., None]
    out = a.copy()
    out[mask] = (np.asarray(color, np.float32) * shade)[mask]
    return out


def feather(mask, px):
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(px))
    return np.asarray(m, np.float32)[..., None] / 255.0


def paint(out, mask, color, blur, alpha=1.0):
    k = feather(mask, blur) * alpha
    return out * (1 - k) + np.asarray(color, np.float32) * k


def stroke(H, W, pts, widths, samples=64):
    """Mask of a tapered brush stroke along a Bezier curve through control points (pixels)."""
    pts = np.array(pts, np.float64)
    t = np.linspace(0, 1, samples)[:, None]
    n = len(pts) - 1
    curve = sum(comb(n, i) * (1 - t) ** (n - i) * t ** i * pts[i] for i in range(n + 1))
    w = np.interp(t[:, 0], np.linspace(0, 1, len(widths)), widths)
    img = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(img)
    for (cx, cy), r in zip(curve, w / 2):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=255)
    return np.asarray(img) > 127


def keep_large(mask, frac):
    lab, n = ndi.label(mask)
    if n == 0:
        return mask
    sizes = ndi.sum(mask, lab, range(1, n + 1))
    return np.isin(lab, 1 + np.nonzero(sizes > frac * mask.size)[0])


def to_lab(rgb):
    c = np.asarray(rgb, np.float32) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]], np.float32)
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883], np.float32)
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def skin_distance(a, tone):
    """Colour distance to a skin tone in Lab, lightness down-weighted (shading must not count as a different colour)."""
    lab, t = to_lab(a), to_lab(np.asarray(tone, np.float32))
    return np.sqrt(((lab[..., 0] - t[0]) * 0.45) ** 2 + (lab[..., 1] - t[1]) ** 2 + (lab[..., 2] - t[2]) ** 2)


def flatten(a, mask, k, sigma, strength=0.5, blur=18, merge=0.0):
    """Reduces the masked area to k flat colours (k-means in Lab, majority-smoothed regions) with broad shading.
    Colours closer than `merge` (Lab) become one (denim fades, folds and highlights are not separate colours).
    Returns the new image and the colours with their regions, most common first."""
    out = a.copy()
    if not mask.any():
        return out, []
    lab = to_lab(a)
    X = lab[mask]
    sample = X[:: max(1, len(X) // 40000)]
    order = np.argsort(sample[:, 0])
    C = sample[order[((np.arange(k) + 0.5) / k * len(sample)).astype(int)]].copy()
    for _ in range(12):
        lbl = ((sample[:, None, :] - C[None]) ** 2).sum(-1).argmin(1)
        for j in range(k):
            if (lbl == j).any():
                C[j] = sample[lbl == j].mean(0)
    labels = ((lab[..., None, :] - C[None, None]) ** 2).sum(-1).argmin(-1)
    if merge > 0:
        # Each colour joins the largest, directly similar colour (no chains: orange never drifts into brown).
        sizes = np.bincount(labels[mask], minlength=k)
        order = np.argsort(-sizes)
        target = np.arange(k)
        for n_, j in enumerate(order):
            for i in order[:n_]:
                if target[i] == i and np.linalg.norm(C[i] - C[j]) < merge:
                    target[j] = i
                    break
        labels = target[labels]
    # Majority smoothing: strands, prints and folds merge into broad regions.
    votes = np.stack([ndi.gaussian_filter(((labels == j) & mask).astype(np.float32), sigma) for j in range(k)], -1)
    labels = votes.argmax(-1)
    l = blur_lum(a, blur)
    colours = []
    for j in np.argsort([-(mask & (labels == j)).sum() for j in range(k)]):
        region = mask & (labels == j)
        if not region.any():
            continue
        col = np.median(a[region], 0)
        shade = np.clip(1.0 + (l - l[region].mean()) * strength, 0.8, 1.15)[..., None]
        out[region] = (col * shade)[region]
        colours.append((col, region))
    return out, colours


# ------------------------------------------------------------------ the cast

def avatar_textures(avatar):
    tex = os.path.join(AVATARS, avatar, "Textures")
    files = os.listdir(tex)
    pick = lambda suffix: next((os.path.join(tex, f) for f in files if f.endswith(suffix)), None)
    return pick("_head_color.png"), pick("_body_color.png"), pick("_opacity_color.png")


def head(name, cfg, feat, head_path):
    src = Image.open(head_path).convert("RGB")
    W, H = src.size
    s = W / 2048.0
    raw = np.asarray(src, np.float32)
    a = np.asarray(smooth(src, 7), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    y, x = yy / H, xx / W
    L = lum(raw)
    P = lambda key: np.array([feat[key][0] * W, (1 - feat[key][1]) * H], np.float32)
    eR, eL = P("eye_R"), P("eye_L")
    D = float(np.linalg.norm(eL - eR))
    mouth = (P("mouth_corner_R") + P("mouth_corner_L") + P("upper_lip") + P("lower_lip")) / 4
    mid = (eR + eL) / 2
    centre = mid + 0.7 * (mouth - mid)
    face = ((xx - centre[0]) / (1.23 * D)) ** 2 + ((yy - centre[1]) / (1.16 * D)) ** 2 < 1.0

    # Skin tone: the cheeks of the original face (each character keeps their own), slightly evened out.
    cheeks = np.zeros((H, W), bool)
    for key in ("cheek_L", "cheek_R"):
        c = P(key)
        cheeks |= (xx - c[0]) ** 2 + (yy - c[1]) ** 2 < (0.07 * D) ** 2
    measured = np.median(a[cheeks], 0).astype(np.float32)
    if cfg.get("skin"):
        tone = np.asarray(cfg["skin"], np.float32)
    else:
        # Harmonised with the reference: halfway to the reference skin's hue at the same brightness, slightly lifted.
        tone = np.clip((0.5 * measured + 0.5 * measured.mean() * REF_SKIN_HUE) * 1.04, 0, 255)

    islands = (x < 0.37) & (y > 0.58) & (L < 0.35)       # eye and mouth-interior islands (dark), not the neck skin
    area = (L > 0.035) & ~islands
    lab_a, _ = ndi.label(area)
    main = lab_a == np.bincount(lab_a[lab_a > 0]).argmax()
    # Hair is told from skin by colour against the character's own (measured) skin. On the main head island it is
    # only allowed above the eyes (plus temples), so neck shadows and stubble never become beards. Separate islands
    # (ponytails, buns, scarf pieces, accessories) are never skin; headscarves are classified freely.
    other = area & ~face & (skin_distance(a, measured) > cfg.get("hair_dE", 14))
    if not cfg.get("scarf"):
        other &= ~main | (yy < mid[1] + 0.45 * D)
    other |= area & ~main & ~(skin_distance(a, measured) < 6)
    # Hair (or headscarf): a clean, rounded edge and no small islands (ear canals, stubble, freckles).
    m = Image.fromarray((other * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(5))
    other = (np.asarray(m.filter(ImageFilter.GaussianBlur(10 * s)), np.float32) > 110) & area & ~face
    if cfg.get("fringe"):
        # A side-swept fringe, lower on the forehead than the original hairline (the main character only).
        fringe_y = np.interp(x, [0.33, 0.40, 0.50, 0.60, 0.67], [0.186, 0.204, 0.200, 0.192, 0.180])
        other |= (np.abs(x - 0.5) < 0.17) & (y < fringe_y) & (y > 0.12)
    other = keep_large(other, 0.004)
    out, colours = flatten(a, other, 3, 8 * s, 0.55, 24 * s)
    # Clusters that are really skin (shaded temples, ears) go back to skin.
    hair_regions = [(c, r) for c, r in colours if skin_distance(c[None, None, :], measured)[0, 0] > 12]
    other = np.zeros_like(other)
    for _, r in hair_regions:
        other |= r
    skin = area & ~other

    # Eye openings and nostrils keep their dark inner rims (and nothing else: the original brows are repainted).
    near_eyes = np.zeros((H, W), bool)
    for e in (eR, eL):
        near_eyes |= (np.abs(xx - e[0]) < 0.3 * D) & (yy - e[1] > -0.15 * D) & (yy - e[1] < 0.12 * D)
    nose = P("nose")
    near_nose = (np.abs(xx - nose[0]) < 0.22 * D) & (yy - nose[1] > -0.05 * D) & (yy - nose[1] < 0.2 * D)
    slit = (L < 0.28) & face & near_eyes
    lab_s, n = ndi.label(slit)
    if n:
        sizes = ndi.sum(slit, lab_s, range(1, n + 1))
        slit = np.isin(lab_s, 1 + np.argsort(sizes)[-2:])  # the two eye openings (not make-up or lashes around them)
    holes = slit | (face & (L < 0.2) & near_nose)
    out[skin & ~holes] = soft_shade(a, skin & ~holes, tone, 0.25, 14 * s)[skin & ~holes]
    out[holes] = a[holes]

    hair = np.asarray(hair_regions[0][0] if hair_regions else (60, 40, 30), np.float32)
    target = np.asarray(cfg.get("hair") or hair, np.float32)
    if not cfg.get("scarf"):
        # One hair colour: every tone follows the main one, within a narrow brightness range (no patchy hair).
        for col, region in hair_regions:
            ratio = float(np.clip(float(lum(col)) / max(float(lum(hair)), 1e-3), 0.85, 1.12))
            out[region] = out[region] / np.maximum(col, 1) * target * ratio
    hair = target

    # Brows: one template for every face, scaled by the eye distance; the height follows the rig's brow bones.
    bh = np.mean([(P(f"eye_{sd}")[1] - (P(f"brow_inner_{sd}")[1] + P(f"brow_outer_{sd}")[1]) / 2) for sd in "LR"]) / (0.247 * D)
    bh = float(np.clip(bh, 0.75, 1.3))
    # Dark brown at most (the reference's brows are dark brown, never black); thinner for adults and girls.
    brow = np.asarray(cfg.get("brow") or np.maximum(hair * 0.42, [56, 38, 27]), np.float32)
    bw = cfg.get("brow_w", {(True, True): 0.85, (True, False): 0.7, (False, True): 1.0, (False, False): 0.85}[
        (name.startswith("Female"), "Child" in name or "Boy" in name)])
    brows = np.zeros((H, W), bool)
    for e, out_dir in ((eR, -1.0), (eL, 1.0)):
        pt = lambda dx, dy: (e[0] + out_dir * dx * D, e[1] + dy * D * bh)
        brows |= stroke(H, W, [pt(0.30, -0.12), pt(0.086, -0.254), pt(-0.32, -0.213)],
                        [0.031 * D * bw, 0.10 * D * bw, 0.107 * D * bw])
    out = paint(out, brows, brow, 1.5 * s)

    # Upper lash line hugging the top of each eye opening, thickest toward the outer corner.
    lash = np.zeros((H, W), bool)
    for e in (eR, eL):
        part = slit & (np.abs(xx - e[0]) < 0.3 * D)
        cols = np.nonzero(part.any(axis=0))[0]
        if len(cols) == 0:
            continue
        top = ndi.median_filter(np.array([np.argmax(part[:, c]) for c in cols], np.float32), 9)
        inner_first = abs(cols[0] - W / 2) < abs(cols[-1] - W / 2)
        f = np.linspace(0, 1, len(cols)) if inner_first else np.linspace(1, 0, len(cols))
        for c, t0, th in zip(cols, top, (0.010 + 0.028 * f ** 0.7) * D):
            lash[int(t0 - th):int(t0 + 0.007 * D), c] = True
    out = paint(out, lash, LASH, 1.0)

    # Mouth: the original lip shape (redder than the surrounding skin) in a muted tone of the skin.
    red = raw[..., 0] - raw[..., 1]
    red_bg = np.asarray(Image.fromarray(np.clip(red, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(30 * s)), np.float32)
    box = (xx > P("mouth_corner_R")[0] - 0.06 * D) & (xx < P("mouth_corner_L")[0] + 0.06 * D) & \
          (yy > P("upper_lip")[1] - 0.12 * D) & (yy < P("lower_lip")[1] + 0.12 * D)
    lips = ndi.binary_opening((red - red_bg > 12) & box, iterations=2)
    out = paint(out, lips, cfg.get("lips") or tone * np.array([0.86, 0.68, 0.66]), 3 * s, 0.75)

    eyeballs(out, W, H, np.asarray(cfg.get("iris", (80, 56, 38)), np.float32), cfg.get("iris_r", 48))
    return out, tone, hair


def eyeballs(out, W, H, iris, iris_r):
    """Bright whites, a large round iris with a dark rim, a black pupil and a small highlight."""
    s = W / 2048.0
    cx, cy = IRIS_UV[0] * W, (1 - IRIS_UV[1]) * H
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.hypot(xx - cx, yy - cy) / s
    island = d < 124
    pupil_r = 16.0
    inner, outer, limbus = iris + (255 - iris) * 0.35, iris * 0.78, iris * 0.32
    t = np.clip((d - pupil_r) / (iris_r - pupil_r), 0, 1)[..., None]
    col = inner * (1 - t) + outer * t
    rim = np.clip((d - (iris_r - 7)) / 4, 0, 1)[..., None]
    col = col * (1 - rim) + limbus * rim
    pupil = np.clip((pupil_r + 1.5 - d) / 3, 0, 1)[..., None]
    col = col * (1 - pupil) + PUPIL * pupil
    white = np.clip((d - iris_r) / 2.5, 0, 1)[..., None]
    back = np.clip((d - 88) / 30, 0, 1)[..., None]
    col = col * (1 - white) + (SCLERA * (1 - back) + np.array([196, 170, 168], np.float32) * back) * white
    hl = np.clip((5.5 - np.hypot(xx - (cx - 11 * s), yy - (cy - 13 * s)) / s) / 2, 0, 1)[..., None]
    col = col * (1 - hl) + 255 * hl
    out[island] = col[island]


def body(name, cfg, tone, body_path):
    src = Image.open(body_path).convert("RGB")
    raw = np.asarray(src, np.float32)
    a = np.asarray(smooth(smooth(src, 9), 5), np.float32)
    if cfg.get("body") == "sweater":
        return sweater(cfg, tone, raw, a)
    W = raw.shape[1]
    bg = lum(raw) < 0.03
    # Skin must match the character's skin in hue and chroma (a*, b*), not only overall: beige fabric has less red.
    lab, t = to_lab(a), to_lab(np.asarray(tone, np.float32))
    tol = cfg.get("skin_dE", 9)
    skin = (np.abs(lab[..., 1] - t[1]) < tol * 0.7) & (np.abs(lab[..., 2] - t[2]) < tol) & (np.abs(lab[..., 0] - t[0]) < 30)
    skin = keep_large(~bg & skin, 0.0005)
    # Specks inside skin (nails, arm hair, shading) are skin; specks of skin colour inside clothes are clothes.
    specks = (~bg & ~skin) & ~keep_large(~bg & ~skin, 0.0006)
    skin |= specks
    cloth = ~bg & ~skin
    # Simple clothes: a few flat colours with broad shading, no prints, fades or fabric texture.
    out, _ = flatten(a, cloth, cfg.get("cloth_k", 6), 9.0 * W / 1024, 0.6, 10, merge=cfg.get("cloth_merge", 22))
    out[skin] = soft_shade(a, skin, tone, 0.7, 6)[skin]
    return out


def sweater(cfg, tone, raw, a):
    """The main character's clothes (Male_Child_01 unwrap): a green crew-neck sweater with wide tonal stripes that
    wrap the torso and sleeves, plain trousers, clean shoes."""
    H, W = raw.shape[:2]
    y, x = np.mgrid[0:H, 0:W] / np.array([H, W], np.float32)[:, None, None]
    r, g, b = raw[..., 0], raw[..., 1], raw[..., 2]
    bg = lum(raw) < 0.03
    blue = (b > r + 25) & (b > g)
    torso = (x > 0.32) & (x < 0.68) & (y > 0.015) & (y < 0.85) & ~bg & (blue | ((y > 0.5) & (y < 0.75)))
    sleeves = ((x < 0.32) | (x > 0.68)) & (y > 0.44) & (y < 0.86) & blue
    jeans = ((x < 0.32) | (x > 0.68)) & (y < 0.44) & ~bg
    hands = (y > 0.84) & ((x < 0.24) | (x > 0.76)) & ~bg & (r > b)
    out = a.copy()
    # The torso unwrap is mirrored about the shoulders (y = 0.48) and the sleeves run along the arm, so bands in V
    # wrap around the body and the arms.
    band_t = np.abs(y - 0.48) * H / 40.0
    band_s = (y - 0.44) * H / 36.0
    dark = (np.floor(np.where(torso, band_t, band_s)) % 2) == 0
    cloth = torso | sleeves
    shade = np.clip(1.0 + (blur_lum(a, 14) - 0.38) * 0.6, 0.85, 1.12)[..., None]
    light, deep = (np.asarray(c, np.float32) for c in cfg["sweater"])
    col = np.where(dark[..., None], deep, light) * shade
    out[cloth] = col[cloth]
    out = soft_shade(out, jeans, cfg["pants"], 0.7, 8)
    out = soft_shade(out, hands, tone, 0.8, 4)
    return out


def cards(path, hair):
    """Hair cards (adults): one clean hair colour with soft shading; the alpha (strand shapes) is kept."""
    im = np.asarray(Image.open(path).convert("RGBA"), np.float32)
    rgb, alpha = im[..., :3], im[..., 3]
    m = alpha > 20
    l = blur_lum(rgb, 3)
    shade = np.clip(l / max(float(l[m].mean()), 1e-3), 0.7, 1.25) ** 0.5
    rgb = np.asarray(hair, np.float32) * shade[..., None]
    return np.dstack([np.clip(rgb, 0, 255), alpha]).astype(np.uint8)


def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name), optimize=True)


def run(name):
    cfg = CAST[name]
    source = cfg.get("source", name)
    with open(os.path.join(FEATURES, f"{source}.json")) as f:
        feat = json.load(f)
    head_path, body_path, cards_path = avatar_textures(source)
    stem = name.lower()
    h, tone, hair = head(name, cfg, feat, head_path)
    save(h, f"{stem}_head.png")
    save(body(name, cfg, tone, body_path), f"{stem}_body.png")
    if cards_path:
        Image.fromarray(cards(cards_path, hair)).save(os.path.join(OUT, f"{stem}_cards.png"), optimize=True)
    style = {"skin": [round(float(v)) for v in tone], "hair": [round(float(v)) for v in hair], "cards": bool(cards_path)}
    with open(os.path.join(FEATURES, f"{name}_style.json"), "w") as f:
        json.dump(style, f)
    print(f"{name}: skin {style['skin']} hair {style['hair']}{' + hair cards' if cards_path else ''}")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for n in (sys.argv[2:] or CAST.keys()):
        run(n)
