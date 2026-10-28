"""Downloads the free (MIT / CC0) source assets for the NUMU realistic worlds (Park, Home, School, Supermarket)
into a staging folder. Files that are already staged are skipped, so re-running only fetches what is new.

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
        # Home: Mum and Dad. School: the teacher. Supermarket: the cashier and a shopper.
        ("Adults", "Female_Adult_06"), ("Adults", "Male_Adult_08"), ("Adults", "Female_Adult_14"), ("Adults", "Female_Adult_12"),
        ("Adults", "Male_Adult_13"),
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
              "f_gestic_listen_neutral_01", "m_gestic_listen_relaxed_01", "f_idle_look_around_01", "m_idle_breathe_01",
              # Indoor worlds: sitting at tables/desks, emotional body language, shopping and teaching.
              "f_sit_table_idle_neutral_01", "m_sit_table_idle_neutral_01", "f_sit_table_idle_relaxed_01", "m_sit_table_idle_relaxed_01",
              "f_sit_table_gestic_thoughtful", "m_sit_table_gestic_thoughtful", "f_gestic_talk_sad_01", "m_gestic_talk_sad_01",
              "f_gestic_listen_sad_01", "f_gestic_talk_nervous_01", "f_idle_nervous_01", "m_idle_angry_01", "m_idle_angry_02",
              "f_gestic_talk_neutral_02", "f_gestic_listen_accept_01", "m_gestic_listen_accept_01", "f_trolley_idle", "m_trolley_idle",
              "f_hold_bag_idle_01", "f_idle_waiting_01", "m_idle_waiting_01", "f_documents_idle"]
    moving = ["f_walk_neutral_01", "m_walk_neutral_01", "f_run_slow_01", "m_run_slow_01"]
    for a in static:
        fetch(f"{RB}/Animations/all_animations_max_motextr_static/{a}.max.fbx", f"{STAGE}/Rocketbox/Animations/{a}.fbx")
    # Turn-in-place and sit-down/stand-up transitions (full root motion, baked into the pose on import).
    transitions = ["f_turn_left_90", "f_turn_right_90", "m_turn_left_90", "m_turn_right_90",
                   "f_sit_down_chair_01", "m_sit_down_chair_01", "f_sit_stand_up_chair_01", "m_sit_stand_up_chair_01"]
    for a in moving:
        fetch(f"{RB}/Animations/all_animations_max_motextr_xy/{a}.max.fbx", f"{STAGE}/Rocketbox/Animations/{a}.fbx")
    for a in transitions:
        fetch(f"{RB}/Animations/all_animations_max_motextr_xyz/{a}.max.fbx", f"{STAGE}/Rocketbox/Animations/{a}.fbx")
    fetch("https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/master/LICENSE.md", f"{STAGE}/Rocketbox/LICENSE.md")
    print("rocketbox animations", len(static) + len(moving) + len(transitions))


PH_PARK = ["painted_wooden_bench", "street_lamp_02", "metal_trash_can", "wooden_picnic_table", "shrub_03", "shrub_04", "fern_02",
           "football", "planter_box_01", "flower_gazania"]
PH_HOME = ["Sofa_01", "modern_arm_chair_01", "coffee_table_round_01", "modern_wooden_cabinet", "round_wooden_table_01", "dining_chair_02",
           "wooden_display_shelves_01", "drawer_cabinet", "potted_plant_01", "potted_plant_02", "potted_plant_04", "throw_pillows_01",
           "hanging_picture_frame_02", "standing_picture_frame_01", "wall_clock", "modern_ceiling_lamp_01", "electric_stove",
           "vintage_electric_kettle", "tea_set_01", "wooden_bowl_01", "wooden_cutting_board", "rubber_duck_toy",
           "chess_set", "ceramic_vase_01"]
PH_SCHOOL = ["SchoolDesk_01", "SchoolChair_01", "metal_office_desk", "steel_frame_shelves_01", "book_encyclopedia_set_01", "binder_notebook",
             "stationery_supplies", "standing_chalkboard_01", "mounted_fluorescent_lights", "wicker_basket_01"]
PH_MARKET = ["wooden_crate_01", "plastic_crate_01", "cardboard_box_01", "long_life_food", "bananas", "food_apple_01", "lemon",
             "food_avocado_01", "food_kiwi_01", "yellow_onion", "sweet_potato", "food_pears_asian_01", "hamburger_buns"]


def polyhaven():
    models = PH_PARK + PH_HOME + PH_SCHOOL + PH_MARKET
    for m in models:
        files = get_json(f"https://api.polyhaven.com/files/{m}")
        fbx = files.get("fbx", {})
        res = next((r for r in ("1k", "2k") if r in fbx), None)
        if res is None:
            print("  skipped (no FBX download):", m)
            continue
        entry = fbx[res]["fbx"]
        fetch(entry["url"], f"{STAGE}/PolyHaven/{m}/{m}.fbx")
        for rel, inc in entry.get("include", {}).items():
            fetch(inc["url"], f"{STAGE}/PolyHaven/{m}/{rel}")
        print("polyhaven model", m)
    hdri = get_json("https://api.polyhaven.com/files/farm_field_puresky")["hdri"]["2k"]["hdr"]
    fetch(hdri["url"], f"{STAGE}/PolyHaven/HDRI/farm_field_puresky_2k.hdr")
    print("polyhaven hdri")


def ambientcg():
    park = ["Grass004", "PavingStones138", "Ground054", "Rubber004", "Bark006", "Bark012", "Leaf001"]
    # Interiors: floors, walls, worktops, upholstery and metal.
    interiors = ["WoodFloor051", "PaintedPlaster017", "Plaster001", "Tiles107", "Tiles040", "Terrazzo005", "Carpet016", "Carpet012",
                 "Fabric030", "Marble012", "Wood049", "Wood051", "Metal032", "Plastic010"]
    for tex in park + interiors:
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
