"""Turns staged source assets into mobile-ready files under Assets/ThirdParty and Assets/NUMU/Worlds.

Usage: python3 -I process_assets.py <staging_dir> <project_dir>
"""
import glob
import math
import os
import random
import shutil
import sys

import numpy as np
from PIL import Image

STAGE, PROJECT = (os.path.abspath(p) for p in sys.argv[1:3])
TP = os.path.join(PROJECT, "Assets", "ThirdParty")
GEN = os.path.join(PROJECT, "Assets", "NUMU", "Worlds", "Textures")


def ensure(path):
    os.makedirs(path, exist_ok=True)
    return path


def done(path):
    """Outputs that already exist are kept, so re-running only processes newly staged assets."""
    return os.path.exists(path) and os.path.getsize(path) > 0


def save_png(img, path, size=None):
    if size and max(img.size) > size:
        img = img.resize((size, size), Image.LANCZOS)
    img.save(path, optimize=True)


# ------------------------------------------------------------------ Rocketbox
def rocketbox():
    out_root = ensure(os.path.join(TP, "Rocketbox"))
    shutil.copy(os.path.join(STAGE, "Rocketbox", "LICENSE.md"), os.path.join(out_root, "LICENSE.md"))
    for avatar_dir in sorted(glob.glob(os.path.join(STAGE, "Rocketbox", "Avatars", "*"))):
        name = os.path.basename(avatar_dir)
        out = ensure(os.path.join(out_root, "Avatars", name))
        tex_out = ensure(os.path.join(out, "Textures"))
        if not done(os.path.join(out, f"{name}_facial.fbx")):
            shutil.copy(os.path.join(avatar_dir, f"{name}_facial.fbx"), os.path.join(out, f"{name}_facial.fbx"))
        for tga in sorted(glob.glob(os.path.join(avatar_dir, "Textures", "*.tga"))):
            base = os.path.splitext(os.path.basename(tga))[0]
            if done(os.path.join(tex_out, base.replace("_specular", "_mask") + ".png")):
                continue
            img = Image.open(tga)
            if base.endswith("_specular"):
                # URP Lit "metallic + smoothness" mask: metal 0, smoothness from the specular level.
                spec = np.asarray(img.convert("L"), dtype=np.float32) / 255.0
                smooth = np.clip(0.18 + spec * 0.55, 0, 1)
                rgba = np.zeros(spec.shape + (4,), dtype=np.uint8)
                rgba[..., 3] = (smooth * 255).astype(np.uint8)
                save_png(Image.fromarray(rgba, "RGBA"), os.path.join(tex_out, base.replace("_specular", "_mask") + ".png"), 1024)
            elif base.endswith("_normal") or base.endswith("_normal_wrinkle"):
                save_png(img.convert("RGB"), os.path.join(tex_out, base + ".png"), 1024)
            else:
                # Faces get read up close during conversations; keep heads at 2K.
                size = 2048 if "_head_" in base else 1024
                save_png(img.convert("RGBA" if img.mode == "RGBA" else "RGB"), os.path.join(tex_out, base + ".png"), size)
        print("rocketbox", name)
    anim_out = ensure(os.path.join(out_root, "Animations"))
    for fbx in glob.glob(os.path.join(STAGE, "Rocketbox", "Animations", "*.fbx")):
        if not done(os.path.join(anim_out, os.path.basename(fbx))):
            shutil.copy(fbx, anim_out)
    print("rocketbox animations copied")


# ------------------------------------------------------------------ Poly Haven
def polyhaven():
    out_root = ensure(os.path.join(TP, "PolyHaven"))
    for model_dir in sorted(glob.glob(os.path.join(STAGE, "PolyHaven", "*"))):
        name = os.path.basename(model_dir)
        if name == "HDRI":
            hdr_out = ensure(os.path.join(out_root, "HDRI"))
            for f in glob.glob(os.path.join(model_dir, "*.hdr")):
                if not done(os.path.join(hdr_out, os.path.basename(f))):
                    shutil.copy(f, hdr_out)
            continue
        out = ensure(os.path.join(out_root, "Models", name))
        for dp, _, files in os.walk(model_dir):
            for f in files:
                if f.endswith(".part") or "_alpha_" in f:
                    continue
                src = os.path.join(dp, f)
                rel = os.path.relpath(src, model_dir)
                dst = os.path.join(out, rel)
                ensure(os.path.dirname(dst))
                alpha = src.replace("_diff_", "_alpha_").rsplit(".", 1)[0] + ".png"
                if done(dst) or done(dst.rsplit(".", 1)[0] + ".png") and "_diff_" in f and os.path.exists(alpha):
                    continue
                if "_diff_" in f and os.path.exists(alpha):
                    # Foliage: fold the separate alpha mask into the colour map for alpha clipping.
                    c = Image.open(src).convert("RGBA")
                    c.putalpha(Image.open(alpha).convert("L").resize(c.size))
                    c.save(dst.rsplit(".", 1)[0] + ".png", optimize=True)
                else:
                    shutil.copy(src, dst)
        print("polyhaven", name)
    with open(os.path.join(out_root, "LICENSE.md"), "w") as f:
        f.write("All Poly Haven assets in this folder are CC0 (public domain). Source: https://polyhaven.com\n")


# ------------------------------------------------------------------ ambientCG materials
def find(folder, suffix):
    hits = glob.glob(os.path.join(folder, f"*_{suffix}.jpg")) + glob.glob(os.path.join(folder, f"*_{suffix}.png"))
    return hits[0] if hits else None


def ambientcg():
    out_root = ensure(os.path.join(TP, "ambientCG"))
    for folder in sorted(glob.glob(os.path.join(STAGE, "ambientCG", "*"))):
        if not os.path.isdir(folder):
            continue
        name = os.path.basename(folder)
        if done(os.path.join(out_root, name, f"{name}_Color.png")):
            continue
        out = ensure(os.path.join(out_root, name))
        color = find(folder, "Color")
        normal = find(folder, "NormalGL")
        rough = find(folder, "Roughness")
        ao = find(folder, "AmbientOcclusion")
        disp = find(folder, "Displacement")
        opacity = find(folder, "Opacity")
        if color:
            c = Image.open(color).convert("RGB")
            if opacity:
                c = c.convert("RGBA")
                c.putalpha(Image.open(opacity).convert("L").resize(c.size))
            save_png(c, os.path.join(out, f"{name}_Color.png"))
        if normal:
            save_png(Image.open(normal).convert("RGB"), os.path.join(out, f"{name}_Normal.png"))
        if rough:
            r = np.asarray(Image.open(rough).convert("L"), dtype=np.float32) / 255.0
            h, w = r.shape
            a = np.asarray(Image.open(ao).convert("L").resize((w, h)), dtype=np.float32) / 255.0 if ao else np.ones_like(r)
            d = np.asarray(Image.open(disp).convert("L").resize((w, h)), dtype=np.float32) / 255.0 if disp else np.full_like(r, 0.5)
            # Terrain/HDRP-style mask: R metallic, G occlusion, B height, A smoothness.
            mask = np.stack([np.zeros_like(r), a, d, 1.0 - r], axis=-1)
            Image.fromarray((mask * 255).astype(np.uint8), "RGBA").save(os.path.join(out, f"{name}_Mask.png"), optimize=True)
        print("ambientcg", name)
    with open(os.path.join(out_root, "LICENSE.md"), "w") as f:
        f.write("All ambientCG materials in this folder are CC0 (public domain). Source: https://ambientcg.com\n")


# ------------------------------------------------------------------ generated foliage atlases
def leaf_cluster():
    if done(os.path.join(GEN, "LeafCluster_Color.png")):
        return
    folder = os.path.join(STAGE, "ambientCG", "Leaf001")
    color = Image.open(find(folder, "Color")).convert("RGB")
    alpha = Image.open(find(folder, "Opacity")).convert("L").resize(color.size)
    normal = Image.open(find(folder, "NormalGL")).convert("RGB").resize(color.size)
    a = np.asarray(alpha)
    # The source holds two leaves side by side; take the left one.
    half = a[:, : a.shape[1] // 2]
    ys, xs = np.where(half > 32)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    leaf_c = color.crop(box)
    leaf_a = alpha.crop(box)
    leaf_n = normal.crop(box)

    rng = random.Random(7)
    size = 1024
    canvas = np.zeros((size, size, 3), np.float32)
    canvas_a = np.zeros((size, size), np.float32)
    canvas_n = np.zeros((size, size, 3), np.float32)
    canvas_n[...] = (0.5, 0.5, 1.0)

    def stamp(scale, angle, cx, cy, tint, shade):
        w = int(leaf_c.width * scale)
        h = int(leaf_c.height * scale)
        lc = leaf_c.resize((w, h), Image.LANCZOS).rotate(angle, resample=Image.BICUBIC, expand=True)
        la = leaf_a.resize((w, h), Image.LANCZOS).rotate(angle, resample=Image.BICUBIC, expand=True)
        ln = leaf_n.resize((w, h), Image.LANCZOS).rotate(angle, resample=Image.BICUBIC, expand=True, fillcolor=(128, 128, 255))
        lc = np.asarray(lc, np.float32) / 255.0 * np.array(tint, np.float32) * shade
        la = np.asarray(la, np.float32) / 255.0
        ln = np.asarray(ln, np.float32) / 255.0 * 2.0 - 1.0
        # Rotating the image rotates its tangent space too: rotate the normal's XY to match.
        t = math.radians(angle)
        nx = ln[..., 0] * math.cos(t) - ln[..., 1] * math.sin(t)
        ny = ln[..., 0] * math.sin(t) + ln[..., 1] * math.cos(t)
        ln = np.stack([nx, ny, ln[..., 2]], axis=-1) * 0.5 + 0.5
        x0 = int(cx - lc.shape[1] / 2)
        y0 = int(cy - lc.shape[0] / 2)
        x1, y1 = max(0, x0), max(0, y0)
        x2, y2 = min(size, x0 + lc.shape[1]), min(size, y0 + lc.shape[0])
        if x2 <= x1 or y2 <= y1:
            return
        sl = (slice(y1 - y0, y2 - y0), slice(x1 - x0, x2 - x0))
        al = la[sl][..., None]
        canvas[y1:y2, x1:x2] = canvas[y1:y2, x1:x2] * (1 - al) + lc[sl] * al
        canvas_n[y1:y2, x1:x2] = canvas_n[y1:y2, x1:x2] * (1 - al) + ln[sl] * al
        canvas_a[y1:y2, x1:x2] = np.maximum(canvas_a[y1:y2, x1:x2], la[sl])

    c = size / 2
    for layer, count, shade in ((0, 380, 0.5), (1, 460, 0.78), (2, 300, 1.0)):
        for _ in range(count):
            r = (rng.random() ** 0.6) * size * (0.43 - layer * 0.04)
            th = rng.random() * math.tau
            cx, cy = c + math.cos(th) * r, c + math.sin(th) * r * 0.9
            # Leaves point outwards from the cluster centre, with some variation.
            ang = -math.degrees(th) - 90 + rng.uniform(-45, 45)
            tint = (rng.uniform(0.82, 1.05), rng.uniform(0.92, 1.08), rng.uniform(0.7, 0.95))
            stamp(rng.uniform(0.045, 0.07) * size / leaf_c.width * 1.6, ang, cx, cy, tint, shade)

    out = ensure(GEN)
    rgba = np.dstack([np.clip(canvas, 0, 1), canvas_a]) * 255
    Image.fromarray(rgba.astype(np.uint8), "RGBA").save(os.path.join(out, "LeafCluster_Color.png"), optimize=True)
    Image.fromarray((np.clip(canvas_n, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(out, "LeafCluster_Normal.png"), optimize=True)
    print("leaf cluster atlas")


def grass_blades():
    if done(os.path.join(GEN, "GrassBlades_Color.png")):
        return
    rng = random.Random(11)
    size = 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = np.zeros((size, size, 4), np.float32)
    for _ in range(140):
        x0 = rng.uniform(0.05, 0.95) * size
        height = rng.uniform(0.45, 0.98) * size
        width = rng.uniform(5, 11)
        bend = rng.uniform(-0.25, 0.25) * height
        base = np.array([rng.uniform(0.16, 0.24), rng.uniform(0.32, 0.42), rng.uniform(0.08, 0.13)])
        tip = np.array([rng.uniform(0.45, 0.62), rng.uniform(0.62, 0.76), rng.uniform(0.2, 0.32)])
        steps = int(height)
        for s in range(steps):
            t = s / steps
            y = size - 1 - s
            cx = x0 + bend * t * t
            w = width * (1 - t) ** 0.8 + 0.6
            xa, xb = int(max(0, cx - w / 2)), int(min(size - 1, cx + w / 2))
            col = base * (1 - t) + tip * t
            for x in range(xa, xb + 1):
                edge = 1 - abs((x - cx) / (w / 2 + 1e-3))
                shade = 0.85 + 0.15 * edge
                px[y, x, :3] = col * shade
                px[y, x, 3] = max(px[y, x, 3], min(1.0, edge * 2.2))
    Image.fromarray((px * 255).astype(np.uint8), "RGBA").save(os.path.join(ensure(GEN), "GrassBlades_Color.png"), optimize=True)
    print("grass blade atlas")


if __name__ == "__main__":
    ambientcg()
    polyhaven()
    rocketbox()
    leaf_cluster()
    grass_blades()
    total = sum(os.path.getsize(os.path.join(dp, f)) for root in (TP, GEN) for dp, _, fs in os.walk(root) for f in fs)
    print(f"done, {total / 1e6:.0f} MB written")
