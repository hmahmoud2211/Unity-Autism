"""Downloads the free (MIT / CC0) source assets for the NUMU Park World into a staging folder.

Usage: python3 -I download_assets.py <staging_dir>
"""
import json
import os
import sys
import time
import urllib.request
import zipfile

UA = {"User-Agent": "NUMU-Unity-Importer/1.0"}
STAGE = os.path.abspath(sys.argv[1])
RB = "https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/Assets"
RB_API = "https://api.github.com/repos/microsoft/Microsoft-Rocketbox/contents/Assets"


def fetch(url, dest, retries=3):
    if os.path.exists(dest) and os.path.getsize(dest) > 0:
        return dest
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    for attempt in range(retries):
        try:
            req = urllib.request.Request(url, headers=UA)
            with urllib.request.urlopen(req, timeout=120) as r, open(dest + ".part", "wb") as f:
                while True:
                    chunk = r.read(1 << 20)
                    if not chunk:
                        break
                    f.write(chunk)
            os.replace(dest + ".part", dest)
            return dest
        except Exception as e:  # network hiccup: retry
            print(f"  retry {attempt + 1} for {url}: {e}")
            time.sleep(2)
    raise RuntimeError(f"failed: {url}")


def get_json(url):
    return json.load(urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60))


def rocketbox():
    avatars = [
        ("Children", "Female_Child_01"), ("Children", "Male_Child_01"), ("Children", "Female_Child_02"), ("Children", "Male_Child_02"),
        ("Adults", "Female_Adult_01"), ("Adults", "Male_Adult_02"),
    ]
    for group, name in avatars:
        base = f"Rocketbox/Avatars/{name}"
        fetch(f"{RB}/Avatars/{group}/{name}/Export/{name}_facial.fbx", f"{STAGE}/{base}/{name}_facial.fbx")
        for item in get_json(f"{RB_API}/Avatars/{group}/{name}/Textures"):
            if item["name"].lower().endswith(".tga"):
                fetch(item["download_url"], f"{STAGE}/{base}/Textures/{item['name']}")
        print("rocketbox avatar", name)
    static = ["f_idle_neutral_01", "m_idle_neutral_01", "f_sit_chair_idle_neutral_01", "m_sit_chair_idle_neutral_01", "f_wave_01", "m_wave_01",
              "f_gestic_laugh_low", "f_cheer_01", "m_cheer_01", "f_claphands_01", "f_gestic_talk_excited_01", "m_gestic_talk_neutral_01",
              "f_gestic_listen_neutral_01", "m_gestic_listen_relaxed_01", "f_idle_look_around_01", "m_idle_breathe_01"]
    moving = ["f_walk_neutral_01", "m_walk_neutral_01", "f_run_slow_01", "m_run_slow_01"]
    for a in static:
        fetch(f"{RB}/Animations/all_animations_max_motextr_static/{a}.max.fbx", f"{STAGE}/Rocketbox/Animations/{a}.fbx")
    for a in moving:
        fetch(f"{RB}/Animations/all_animations_max_motextr_xy/{a}.max.fbx", f"{STAGE}/Rocketbox/Animations/{a}.fbx")
    fetch("https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/LICENSE.md", f"{STAGE}/Rocketbox/LICENSE.md")
    print("rocketbox animations", len(static) + len(moving))


def polyhaven():
    models = ["painted_wooden_bench", "street_lamp_02", "metal_trash_can", "wooden_picnic_table", "shrub_03", "shrub_04", "fern_02",
              "football", "planter_box_01", "flower_gazania"]
    for m in models:
        files = get_json(f"https://api.polyhaven.com/files/{m}")
        entry = files["fbx"]["1k"]["fbx"]
        fetch(entry["url"], f"{STAGE}/PolyHaven/{m}/{m}.fbx")
        for rel, inc in entry.get("include", {}).items():
            fetch(inc["url"], f"{STAGE}/PolyHaven/{m}/{rel}")
        print("polyhaven model", m)
    hdri = get_json("https://api.polyhaven.com/files/farm_field_puresky")["hdri"]["2k"]["hdr"]
    fetch(hdri["url"], f"{STAGE}/PolyHaven/HDRI/farm_field_puresky_2k.hdr")
    print("polyhaven hdri")


def ambientcg():
    for tex in ["Grass004", "PavingStones138", "Ground054", "Rubber004", "Bark006", "Bark012", "Leaf001"]:
        z = fetch(f"https://ambientcg.com/get?file={tex}_1K-JPG.zip", f"{STAGE}/ambientCG/{tex}_1K-JPG.zip")
        out = f"{STAGE}/ambientCG/{tex}"
        os.makedirs(out, exist_ok=True)
        with zipfile.ZipFile(z) as zf:
            for member in zf.namelist():
                # Only plain file names: never let an archive write outside its folder.
                name = os.path.basename(member)
                if not name or name.startswith(".") or not name.lower().endswith((".jpg", ".png")):
                    continue
                with zf.open(member) as src, open(os.path.join(out, name), "wb") as dst:
                    dst.write(src.read())
        print("ambientcg", tex)


if __name__ == "__main__":
    for step in (ambientcg, polyhaven, rocketbox):
        step()
    total = sum(os.path.getsize(os.path.join(dp, f)) for dp, _, fs in os.walk(STAGE) for f in fs)
    print(f"done, staged {total / 1e6:.0f} MB in {STAGE}")
