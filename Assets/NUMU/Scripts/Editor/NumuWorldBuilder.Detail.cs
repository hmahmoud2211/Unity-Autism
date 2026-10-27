using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Numu.EditorTools
{
    /// <summary>Detailed island dressing. All coordinates are reference-image pixels (x right, y down).</summary>
    public static partial class NumuWorldBuilder
    {
        static readonly List<(Vector3 p, float r)> _keepClear = new List<(Vector3, float)>();
        static readonly List<(Vector3 c, float rx, float rz)> _keepClearEllipses = new List<(Vector3, float, float)>();
        static readonly Dictionary<string, NumuIsland> _islands = new Dictionary<string, NumuIsland>();
        static readonly Dictionary<string, MeshBuilder> _flora = new Dictionary<string, MeshBuilder>();
        static readonly Dictionary<string, MeshBuilder> _paths = new Dictionary<string, MeshBuilder>();
        static readonly Dictionary<string, MeshBuilder> _fences = new Dictionary<string, MeshBuilder>();

        static void KeepClear(Vector3 p, float r) => _keepClear.Add((p, r));

        static bool IsClear(Vector3 p, float r)
        {
            foreach (var k in _keepClear)
                if (new Vector2(k.p.x - p.x, k.p.z - p.z).magnitude < k.r + r) return false;
            foreach (var e in _keepClearEllipses)
            {
                float dx = (p.x - e.c.x) / (e.rx + r), dz = (p.z - e.c.z) / (e.rz + r);
                if (dx * dx + dz * dz < 1f) return false;
            }
            return true;
        }

        static Vector3 Ground(Vector3 p) => GroundAt(p, out var hit, out _) ? hit : p;

        static MeshBuilder Flora(string id) => _flora.TryGetValue(id, out var m) ? m : (_flora[id] = new MeshBuilder());
        static MeshBuilder PathMb(string id) => _paths.TryGetValue(id, out var m) ? m : (_paths[id] = new MeshBuilder());
        static MeshBuilder FenceMb(string id) => _fences.TryGetValue(id, out var m) ? m : (_fences[id] = new MeshBuilder());

        static Transform Sub(string islandId, string name)
        {
            var root = _islands[islandId].transform;
            var t = root.Find(name);
            return t != null ? t : Group(name, root);
        }

        // ------------------------------------------------------------------ entry

        static partial void BuildDetailedContentImpl(Transform parent)
        {
            _islands.Clear();
            _flora.Clear();
            _paths.Clear();
            _fences.Clear();
            _keepClearEllipses.Clear();

            NumuTextureKit.GenerateAll();
            BuildPrefabs();
            CreateIslandRoots();

            BuildHome();
            BuildSchool();
            BuildPark();
            BuildSupermarket();
            BuildHub();
            BuildBackgroundRock();

            BuildRimVegetation();
            FillInteriors();
            DecorateIslets();
            FlushIslandMeshes();
            ComputeFocusPoses();
        }

        static void CreateIslandRoots()
        {
            var islandsParent = Group("Learning Islands", _world);
            void Make(string id, string name, string subtitle, Color theme, Color dark, bool levels)
            {
                var go = new GameObject($"Island_{name.Replace(" ", "")}");
                go.transform.SetParent(islandsParent, false);
                var isl = go.AddComponent<NumuIsland>();
                isl.islandId = id;
                isl.displayName = name;
                isl.subtitle = subtitle;
                isl.themeColor = theme;
                isl.themeDark = dark;
                isl.hasLevels = levels;
                _islands[id] = isl;
            }
            foreach (var info in NumuData.Islands) Make(info.Id, info.Name, info.Subtitle, info.Theme, info.Title, true);
            Make("hub", "NUMU Hub", "Your starting point", NumuColor.Hex("#2fa58d"), NumuColor.Hex("#1d6e5e"), false);

            // Link terrain tops to their island for clicks.
            foreach (var def in IslandDefs)
            {
                if (def.Owner == null || !IslandRoots.TryGetValue(def.Id, out var t)) continue;
                var top = t.Find("Top");
                if (top != null) top.gameObject.AddComponent<NumuIslandLink>().islandId = def.Owner;
            }
        }

        // ------------------------------------------------------------------ placement helpers

        static GameObject PlaceWidth(string prefab, Transform parent, float px, float py, float widthPx, float yaw, float sink = 0.1f)
        {
            Vector3 b = G(px, py);
            float scale = widthPx * U(px, py, b.y) / PrefabWidth(prefab);
            var go = Instance(prefab, parent, b + Vector3.down * sink, yaw, scale);
            KeepClear(b, PrefabWidth(prefab) * scale * 0.5f);
            return go;
        }

        /// <summary>Places a building so its front wall (not its centre) sits on the reference base line.</summary>
        static GameObject PlaceBuilding(string prefab, Transform parent, float px, float py, float widthPx, float yaw, float frontDepth)
        {
            Vector3 b = G(px, py);
            float scale = widthPx * U(px, py, b.y) / PrefabWidth(prefab);
            Quaternion r = Quaternion.Euler(0, yaw, 0);
            Vector3 pos = b + r * new Vector3(0, -0.12f, frontDepth * scale);
            var go = Instance(prefab, parent, pos, yaw, scale);
            KeepClear(pos, PrefabWidth(prefab) * scale * 0.5f);
            return go;
        }

        static GameObject PlaceHeight(string prefab, Transform parent, float px, float py, float heightPx, float yaw, float sink = 0.05f)
        {
            Vector3 b = G(px, py);
            float h = HeightFromPixels(px, py, py - heightPx, b.y);
            float scale = h / PrefabHeight(prefab);
            var go = Instance(prefab, parent, b + Vector3.down * sink, yaw, scale);
            KeepClear(b, PrefabWidth(prefab) * scale * 0.45f);
            return go;
        }

        static int _treeCounter;

        static void Tree(string islandId, string kind, float px, float py, float heightPx)
        {
            string prefab = kind switch
            {
                "pine" => $"Tree_Pine_{_treeCounter % 3}",
                "blossom" => $"Tree_Blossom_{_treeCounter % 3}",
                "yellow" => $"Tree_Yellow_{_treeCounter % 2}",
                _ => $"Tree_Round_{_treeCounter % 4}",
            };
            _treeCounter++;
            PlaceHeight(prefab, Sub(islandId, "Trees"), px, py, heightPx, (_treeCounter * 137) % 360, 0.15f);
        }

        static void Markers(string islandId, (float x, float y)[] pts)
        {
            var info = NumuData.Get(islandId);
            var parent = Sub(islandId, "Level Markers");
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 b = G(pts[i].x, pts[i].y);
                float radius = 21f * U(pts[i].x, pts[i].y, b.y);
                var go = Instance("LevelMarker", parent, b + Vector3.up * 0.02f, 0f, radius);
                go.name = $"Level {i + 1}";
                var m = go.GetComponent<NumuLevelMarker>();
                m.islandId = islandId;
                m.level = i + 1;
                m.title = info.Levels[i];
                m.themeColor = info.Theme;
                m.number.text = (i + 1).ToString();
                m.Refresh();
                PrefabUtility.RecordPrefabInstancePropertyModifications(m);
                PrefabUtility.RecordPrefabInstancePropertyModifications(m.number);
                _islands[islandId].markers.Add(m);
                KeepClear(b, radius * 1.3f);
                // Keep the camera-facing side open too, so nothing tall hides the badge.
                KeepClear(b + Vector3.back * radius * 2.2f, radius * 1.4f);
            }
        }

        static void StonePath(string islandId, (float x, float y)[] px, float widthPx, int seed, bool grey = false)
        {
            var ctrl = new List<Vector3>();
            foreach (var p in px) ctrl.Add(G(p.x, p.y));
            float w = widthPx * U(px[0].x, px[0].y, ctrl[0].y);
            PathKit.StonePath(PathMb(islandId), ctrl, w, seed, Ground, grey ? PathKit.GreyCobbles : null);
            foreach (var p in Shapes.SampleSpline(ctrl, 1f)) KeepClear(p, w * 0.5f + 0.2f);
        }

        static void Plaza(string islandId, float cx, float cy, float rxPx, float rzPx, int seed, bool grey = false)
        {
            Vector3 c = G(cx, cy);
            float rx = Vector3.Distance(P(cx - rxPx, cy, c.y), P(cx + rxPx, cy, c.y)) * 0.5f;
            float rz = Vector3.Distance(P(cx, cy - rzPx, c.y), P(cx, cy + rzPx, c.y)) * 0.5f;
            PathKit.Plaza(PathMb(islandId), c, rx, rz, seed, Ground, grey ? PathKit.GreyCobbles : null);
            _keepClearEllipses.Add((c, rx, rz));
        }

        static void Flowers(string islandId, float px, float py, float rPx, int count, int seed)
        {
            Vector3 c = G(px, py);
            float r = rPx * U(px, py, c.y);
            NatureKit.FlowerPatch(Flora(islandId), c, r, count, seed, null, Ground);
        }

        static void Fence(string islandId, bool picket, int seed, params (float x, float y)[] px)
        {
            var pts = new List<Vector3>();
            foreach (var p in px) pts.Add(G(p.x, p.y));
            PropKit.Fence(FenceMb(islandId), pts, picket, seed);
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k <= 4; k++) KeepClear(Vector3.Lerp(pts[i], pts[i + 1], k / 4f), 0.6f);
        }

        // ------------------------------------------------------------------ signs

        static void Sign(string islandId, string id, float cx, float cy, float wpx, float hpx, float anchorPy, string icon, string text, int seed)
        {
            Vector3 anchor = G(cx, anchorPy);
            Vector3 c = PVertical(cx, cy, anchor);
            float w = wpx * U(cx, cy, c.y);
            float h = PVertical(cx, cy - hpx * 0.5f, anchor).y - PVertical(cx, cy + hpx * 0.5f, anchor).y;
            float boardY = c.y - anchor.y;

            var root = new GameObject($"Sign_{id}");
            var mb = new MeshBuilder();
            PropKit.SignBoard(mb, w, h, boardY, seed, 0.6f);
            Part(root.transform, "Board", mb, Materials.Vertex, "Prefabs");

            float iconSize = h * 0.78f;
            var iconGo = Decal(root.transform, "Icon", new Vector3(-w * 0.5f + iconSize * 0.62f, boardY, -0.29f), new Vector2(iconSize, iconSize),
                NumuTextureKit.Texture(icon), $"M_SignIcon_{id}");

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            float left = -w * 0.5f + iconSize * 1.2f;
            float right = w * 0.5f - h * 0.18f;
            textGo.transform.localPosition = new Vector3((left + right) * 0.5f, boardY + h * 0.02f, -0.3f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.fontSharedMaterial = _signTextMat;
            tmp.text = text;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 1f;
            tmp.fontSizeMax = 200f;
            tmp.lineSpacing = -18f;
            tmp.characterSpacing = 2f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(right - left, h * 0.78f);
            tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, boardY, 0);
            col.size = new Vector3(w + 0.4f, h + 0.4f, 0.8f);
            root.AddComponent<NumuIslandLink>().islandId = islandId;
            var signPrefab = SavePrefab(root);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(signPrefab, Sub(islandId, "Sign"));
            inst.transform.position = new Vector3(c.x, anchor.y - 0.3f, anchor.z);
            // Keep the posts and the whole camera-facing area in front of the board free of trees.
            for (int k = 0; k <= 4; k++)
            {
                float x = Mathf.Lerp(-w * 0.5f, w * 0.5f, k / 4f);
                KeepClear(anchor + Vector3.right * x, 1.0f);
                KeepClear(anchor + Vector3.right * x + Vector3.back * 3.0f, 2.2f);
            }
        }

        // ------------------------------------------------------------------ islands

        static void BuildHome()
        {
            const string id = "home";
            var b = Sub(id, "Buildings");
            var props = Sub(id, "Props");
            PlaceBuilding("Building_HomeCottage", b, 386, 166, 166, -6f, 3.2f);
            PlaceBuilding("Building_SmallHouse", b, 182, 220, 108, 14f, 1.9f);
            PlaceHeight("Mascot_Fox", Sub(id, "Mascot"), 298, 258, 88, -12f);
            Sign(id, "Home", 196, 156, 208, 63, 238, "Icon_Home", "Home", 901);

            Markers(id, new[] { (280f, 325f), (345f, 287f), (403f, 343f), (494f, 345f), (464f, 240f) });
            StonePath(id, new[] { (300f, 392f), (282f, 326f), (345f, 287f), (410f, 262f), (464f, 240f), (448f, 205f), (428f, 180f) }, 32, 31);
            StonePath(id, new[] { (345f, 287f), (403f, 343f), (494f, 345f), (552f, 343f) }, 30, 32);
            StonePath(id, new[] { (282f, 326f), (230f, 300f), (198f, 248f) }, 26, 33);

            PlaceWidth("Prop_Swing", props, 492, 276, 62, 0f);
            PlaceHeight("Prop_Mailbox", props, 222, 294, 40, 10f);
            PlaceWidth("Prop_Parasol", props, 176, 276, 58, 0f);
            PlaceWidth("Prop_Planter", props, 446, 344, 70, -4f);
            PlaceWidth("Prop_Planter", props, 520, 300, 50, 20f);
            Fence(id, true, 1, (116f, 304f), (140f, 326f), (206f, 343f));
            Fence(id, true, 2, (362f, 170f), (398f, 176f), (434f, 182f));
            Fence(id, true, 3, (468f, 192f), (510f, 206f));

            Tree(id, "pine", 55, 205, 95); Tree(id, "pine", 30, 265, 62); Tree(id, "round", 98, 252, 82); Tree(id, "round", 140, 266, 58);
            Tree(id, "round", 76, 304, 52); Tree(id, "round", 300, 82, 95); Tree(id, "round", 462, 74, 110); Tree(id, "round", 236, 140, 62);
            Tree(id, "pine", 522, 114, 72); Tree(id, "round", 550, 220, 78); Tree(id, "round", 556, 290, 58); Tree(id, "pine", 338, 322, 50);
            Tree(id, "pine", 405, 248, 48); Tree(id, "pine", 160, 205, 55); Tree(id, "round", 120, 160, 60); Tree(id, "pine", 585, 262, 48);
            Tree(id, "round", 440, 395, 34); Tree(id, "round", 150, 425, 34);

            Flowers(id, 150, 300, 26, 22, 41); Flowers(id, 252, 362, 20, 14, 42); Flowers(id, 330, 372, 18, 12, 43); Flowers(id, 522, 252, 20, 14, 44);
            Flowers(id, 442, 214, 15, 10, 45); Flowers(id, 206, 330, 15, 10, 46); Flowers(id, 480, 312, 16, 12, 47); Flowers(id, 112, 336, 18, 12, 48);
            Flowers(id, 300, 220, 14, 10, 49); Flowers(id, 400, 418, 14, 10, 50);
        }

        static void BuildSchool()
        {
            const string id = "school";
            var props = Sub(id, "Props");
            PlaceBuilding("Building_School", Sub(id, "Buildings"), 805, 154, 180, 0f, 2.7f);
            PlaceHeight("Mascot_Penguin", Sub(id, "Mascot"), 727, 197, 88, 12f);
            Sign(id, "School", 625, 52, 226, 67, 162, "Icon_School", "School", 902);

            Plaza(id, 795, 205, 170, 50, 51, true);
            StonePath(id, new[] { (614f, 238f), (650f, 205f), (700f, 206f) }, 28, 52, true);
            StonePath(id, new[] { (860f, 232f), (884f, 260f) }, 26, 53, true);
            Markers(id, new[] { (660f, 175f), (775f, 212f), (838f, 238f), (918f, 192f), (948f, 228f) });

            PlaceWidth("Prop_SchoolBus", props, 915, 150, 94, 0f);
            PlaceWidth("Prop_Blackboard", props, 648, 166, 54, 12f);
            PlaceWidth("Prop_Desk", props, 846, 196, 34, 0f);
            PlaceWidth("Prop_Desk", props, 878, 206, 30, -8f);
            PlaceWidth("Prop_Bench", props, 838, 160, 42, 0f);
            PlaceWidth("Prop_Bench", props, 604, 198, 34, 35f);

            Tree(id, "pine", 595, 110, 62); Tree(id, "pine", 612, 170, 48); Tree(id, "pine", 700, 120, 52); Tree(id, "round", 676, 132, 44);
            Tree(id, "pine", 962, 130, 62); Tree(id, "pine", 986, 182, 46); Tree(id, "round", 906, 104, 52); Tree(id, "round", 682, 254, 40);
            Tree(id, "round", 590, 232, 40); Tree(id, "pine", 740, 108, 40);

            Flowers(id, 690, 240, 18, 12, 61); Flowers(id, 930, 238, 14, 10, 62); Flowers(id, 624, 210, 14, 10, 63); Flowers(id, 878, 122, 14, 10, 64);
            Flowers(id, 742, 146, 12, 8, 65); Flowers(id, 960, 205, 12, 8, 66);
        }

        static void BuildPark()
        {
            const string id = "park";
            var props = Sub(id, "Props");
            PlaceWidth("Prop_Playground", props, 1128, 198, 150, 0f);
            PlaceWidth("Prop_Fountain", props, 1225, 230, 84, 0f);
            PlaceHeight("Mascot_Bunny", Sub(id, "Mascot"), 1262, 142, 88, -10f);
            Sign(id, "Park", 1121, 75, 207, 74, 132, "Icon_Park", "Park", 903);

            Plaza(id, 1225, 232, 66, 26, 71);
            StonePath(id, new[] { (1012f, 300f), (1065f, 300f), (1115f, 272f), (1157f, 266f), (1195f, 248f) }, 30, 72);
            StonePath(id, new[] { (1258f, 245f), (1325f, 222f), (1352f, 200f) }, 28, 73);
            StonePath(id, new[] { (1222f, 205f), (1205f, 165f), (1240f, 148f) }, 26, 74);
            StonePath(id, new[] { (1192f, 222f), (1115f, 213f), (1080f, 180f) }, 26, 75);
            Markers(id, new[] { (1325f, 220f), (1115f, 213f), (1065f, 300f), (1157f, 266f), (1200f, 160f) });

            PlaceWidth("Prop_Gazebo", props, 1408, 182, 60, 0f);
            PlaceWidth("Prop_Swing", props, 1268, 292, 56, 0f);
            PlaceWidth("Prop_Bench", props, 1070, 245, 40, 25f);
            PlaceWidth("Prop_Bench", props, 1340, 198, 38, -10f);
            PlaceWidth("Prop_Bench", props, 1395, 270, 40, -25f);
            PlaceHeight("Prop_Lamp", props, 1372, 200, 58, 0f);
            PlaceHeight("Prop_Lamp", props, 1022, 258, 50, 0f);
            Fence(id, false, 4, (1345f, 292f), (1385f, 280f), (1420f, 262f));
            Fence(id, false, 5, (1080f, 312f), (1130f, 318f));

            Tree(id, "blossom", 1040, 205, 72); Tree(id, "blossom", 1342, 145, 78); Tree(id, "blossom", 1430, 250, 78); Tree(id, "blossom", 1003, 192, 50);
            Tree(id, "round", 1075, 134, 62); Tree(id, "pine", 1100, 122, 46); Tree(id, "round", 1178, 108, 52); Tree(id, "pine", 1020, 292, 42);
            Tree(id, "round", 1306, 308, 36); Tree(id, "pine", 1172, 302, 38); Tree(id, "blossom", 1290, 112, 50);

            Flowers(id, 1082, 282, 18, 14, 81); Flowers(id, 1182, 292, 15, 10, 82); Flowers(id, 1292, 252, 18, 14, 83); Flowers(id, 1362, 242, 15, 10, 84);
            Flowers(id, 1242, 192, 15, 10, 85); Flowers(id, 1150, 142, 15, 10, 86); Flowers(id, 1402, 232, 15, 10, 87); Flowers(id, 1030, 256, 15, 10, 88);
            Flowers(id, 1260, 290, 12, 10, 89);
        }

        static void BuildSupermarket()
        {
            const string id = "supermarket";
            var props = Sub(id, "Props");
            PlaceBuilding("Building_Supermarket", Sub(id, "Buildings"), 1332, 462, 282, 0f, 2.0f);
            PlaceHeight("Mascot_Bear", Sub(id, "Mascot"), 1418, 472, 98, -14f);
            Sign(id, "Supermarket", 1438, 325, 227, 75, 408, "Icon_Supermarket", "Supermarket", 904);

            Plaza(id, 1282, 515, 235, 62, 91);
            StonePath(id, new[] { (1046f, 488f), (1100f, 478f), (1140f, 492f) }, 30, 92);
            StonePath(id, new[] { (1400f, 560f), (1432f, 598f) }, 28, 93);
            Markers(id, new[] { (1100f, 476f), (1180f, 511f), (1268f, 530f), (1365f, 543f), (1470f, 565f) });

            PlaceWidth("Prop_Cart", props, 1352, 480, 54, -25f);
            PlaceWidth("Prop_ProduceStand", props, 1192, 468, 82, 10f);
            PlaceWidth("Prop_FruitCrate_0", props, 1158, 490, 40, 10f);
            PlaceWidth("Prop_FruitCrate_1", props, 1226, 490, 38, -5f);
            PlaceWidth("Prop_FruitCrate_2", props, 1255, 472, 34, 5f);
            PlaceWidth("Prop_Stall", props, 1245, 580, 64, 0f);
            PlaceHeight("Prop_Lamp", props, 1090, 404, 70, 0f);
            PlaceHeight("Prop_Lamp", props, 1527, 450, 62, 0f);
            PlaceHeight("Prop_ChalkSign", props, 1130, 434, 42, 15f);
            PlaceWidth("Prop_Crates", props, 1478, 536, 40, 10f);
            PlaceWidth("Prop_Shelf", props, 1494, 480, 40, -20f);

            Tree(id, "yellow", 1065, 388, 58); Tree(id, "round", 1112, 370, 48); Tree(id, "yellow", 1500, 524, 64); Tree(id, "round", 1548, 428, 52);
            Tree(id, "pine", 1292, 568, 46); Tree(id, "pine", 1122, 532, 42); Tree(id, "round", 1460, 400, 46); Tree(id, "yellow", 1552, 548, 42);
            Tree(id, "pine", 1062, 532, 38);

            Flowers(id, 1150, 545, 18, 14, 101); Flowers(id, 1345, 590, 15, 10, 102); Flowers(id, 1470, 512, 15, 10, 103); Flowers(id, 1220, 562, 15, 10, 104);
            Flowers(id, 1530, 500, 12, 8, 105); Flowers(id, 1082, 448, 15, 10, 106); Flowers(id, 1400, 600, 12, 8, 107);
        }

        static void BuildHub()
        {
            const string id = "hub";
            var props = Sub(id, "Props");
            PlaceBuilding("Building_HubCottage", Sub(id, "Buildings"), 777, 455, 116, 0f, 2.1f);
            Sign(id, "Hub", 771, 490, 157, 70, 548, "Icon_Hub", "NUMU\nHub", 905);

            Plaza(id, 785, 470, 66, 18, 111);
            StonePath(id, new[] { (666f, 448f), (702f, 474f) }, 26, 112);
            StonePath(id, new[] { (892f, 430f), (858f, 455f) }, 26, 113);
            StonePath(id, new[] { (918f, 494f), (868f, 500f) }, 26, 114);
            StonePath(id, new[] { (776f, 535f), (776f, 556f) }, 26, 115);

            // Sailboat moored by the dock.
            Vector3 boatPos = P(858, 648, 0.1f);
            float boatH = HeightFromPixels(858, 648, 532, 0.1f);
            var boat = Instance("Prop_Sailboat", props, new Vector3(boatPos.x, 0.05f, boatPos.z), -64f, boatH / PrefabHeight("Prop_Sailboat"));
            boat.name = "Sailboat";
            PlaceHeight("Prop_Lamp", props, 700, 480, 46, 0f);

            Tree(id, "pine", 690, 430, 42); Tree(id, "round", 856, 414, 46); Tree(id, "pine", 884, 452, 36); Tree(id, "round", 706, 506, 32);
            Tree(id, "pine", 862, 528, 30); Tree(id, "round", 650, 472, 34);
            Flowers(id, 722, 506, 15, 10, 121); Flowers(id, 850, 516, 15, 10, 122); Flowers(id, 744, 540, 12, 8, 123); Flowers(id, 812, 540, 12, 8, 124);
        }

        static void BuildBackgroundRock()
        {
            // Snow-capped rock pillar between Home and School, standing in the water.
            Vector3 b = P(553, 165, 0f);
            float h = HeightFromPixels(553, 165, 128, 0f) + 1.5f;
            var go = Instance("Rock_SnowCap", Sub("school", "Props"), b + Vector3.down * 1.5f, 20f, h / PrefabHeight("Rock_SnowCap"));
            go.name = "SnowCapRock";
        }

        // ------------------------------------------------------------------ edges and islets

        static void BuildRimVegetation()
        {
            var rng = new System.Random(12345);
            foreach (var def in IslandDefs)
            {
                if (!IslandShapes.TryGetValue(def.Id, out var shape)) continue;
                string owner = def.Owner ?? "hub";
                var flora = Flora(owner);
                float step = def.Id.Contains("Upper") ? 3.2f : 2.1f;
                int samples = Mathf.Max(6, Mathf.RoundToInt(shape.Perimeter / step));
                for (int i = 0; i < samples; i++)
                {
                    shape.Sample((i + (float)rng.NextDouble() * 0.4f) / samples, out var rim, out var o);
                    Vector3 p = Ground(rim - o * (0.9f + (float)rng.NextDouble() * 0.5f));
                    double roll = rng.NextDouble();
                    float size = 0.9f + (float)rng.NextDouble() * 0.6f;
                    if (!IsClear(p, size * 0.6f)) continue;
                    if (roll < 0.58)
                    {
                        NatureKit.Bush(flora, p, size, rng.Next(99999), rng.NextDouble() < 0.4 ? NatureKit.Hedge : NatureKit.Green, rng.NextDouble() < 0.45 ? NatureKit.FlowerColors : null);
                        KeepClear(p, size * 0.5f);
                    }
                    else if (roll < 0.85)
                    {
                        NatureKit.FlowerPatch(flora, p, 0.9f, 6, rng.Next(99999), null, Ground);
                    }
                    else if (roll < 0.93 && !def.Id.Contains("Upper"))
                    {
                        Vector3 tp = p;
                        var go = Instance(rng.NextDouble() < 0.5 ? $"Tree_Round_{rng.Next(4)}" : $"Tree_Pine_{rng.Next(3)}", Sub(owner, "Trees"), tp + Vector3.down * 0.1f, rng.Next(360), (2.4f + (float)rng.NextDouble() * 1.2f) / TreeNominalHeight);
                        KeepClear(tp, 1.2f);
                    }
                }
                // Scattered grass tufts and pebbles on the top.
                var bounds = new Bounds(shape.Rim[0], Vector3.zero);
                foreach (var r in shape.Rim) bounds.Encapsulate(r);
                int tufts = Mathf.RoundToInt(bounds.size.x * bounds.size.z * 0.05f);
                for (int k = 0; k < tufts; k++)
                {
                    Vector3 q = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble()), 0, Mathf.Lerp(bounds.min.z, bounds.max.z, (float)rng.NextDouble()));
                    if (!GroundAt(q, out var hit, out _) || Mathf.Abs(hit.y - def.Top) > 1.2f || !IsClear(hit, 0.3f)) continue;
                    if (rng.NextDouble() < 0.8) NatureKit.GrassTuft(flora, hit, 0.45f + (float)rng.NextDouble() * 0.3f, rng.Next(99999));
                    else NatureKit.Stone(flora, hit, 0.18f + (float)rng.NextDouble() * 0.15f, rng.Next(99999));
                }
            }
        }

        /// <summary>Extra trees and bushes on open grass so island tops read as lush as the reference.</summary>
        static void FillInteriors()
        {
            var rng = new System.Random(4242);
            var budget = new Dictionary<string, int> { { "Home", 16 }, { "School", 6 }, { "Park", 12 }, { "Supermarket", 8 }, { "Hub", 3 } };
            foreach (var kv in budget)
            {
                var def = System.Array.Find(IslandDefs, d => d.Id == kv.Key);
                var shape = IslandShapes[kv.Key];
                string owner = def.Owner;
                var bounds = new Bounds(shape.Rim[0], Vector3.zero);
                foreach (var r in shape.Rim) bounds.Encapsulate(r);
                int placed = 0;
                for (int attempt = 0; attempt < 400 && placed < kv.Value; attempt++)
                {
                    Vector3 q = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, (float)rng.NextDouble()), 0, Mathf.Lerp(bounds.min.z, bounds.max.z, (float)rng.NextDouble()));
                    if (!GroundAt(q, out var hit, out _) || Mathf.Abs(hit.y - def.Top) > 0.8f) continue;
                    // Stay off the rim band (handled by rim vegetation) and away from everything registered.
                    int ri = shape.ClosestRimIndex(hit);
                    if (new Vector2(shape.Rim[ri].x - hit.x, shape.Rim[ri].z - hit.z).magnitude < 2.2f) continue;
                    bool tree = rng.NextDouble() < 0.55;
                    float clearance = tree ? 1.5f : 0.9f;
                    if (!IsClear(hit, clearance)) continue;
                    if (tree)
                    {
                        string prefab = owner == "park" && rng.NextDouble() < 0.5 ? $"Tree_Blossom_{rng.Next(3)}"
                            : owner == "supermarket" && rng.NextDouble() < 0.5 ? $"Tree_Yellow_{rng.Next(2)}"
                            : rng.NextDouble() < 0.4 ? $"Tree_Pine_{rng.Next(3)}" : $"Tree_Round_{rng.Next(4)}";
                        float h = 2.8f + (float)rng.NextDouble() * 1.6f;
                        Instance(prefab, Sub(owner, "Trees"), hit + Vector3.down * 0.12f, rng.Next(360), h / TreeNominalHeight);
                    }
                    else
                    {
                        NatureKit.Bush(Flora(owner), hit, 0.8f + (float)rng.NextDouble() * 0.5f, rng.Next(99999), NatureKit.Green, rng.NextDouble() < 0.5 ? NatureKit.FlowerColors : null);
                    }
                    KeepClear(hit, clearance);
                    placed++;
                }
            }
        }

        static void DecorateIslets()
        {
            var rng = new System.Random(777);
            var parent = Group("Islet Dressing", _world);
            var flora = new MeshBuilder();
            foreach (var kv in IslandShapes)
            {
                if (!kv.Key.StartsWith("Islet_")) continue;
                var shape = kv.Value;
                Vector3 c = Ground(shape.Centroid);
                float r = 0f;
                foreach (var p in shape.Rim) r = Mathf.Max(r, new Vector2(p.x - c.x, p.z - c.z).magnitude);
                int bushes = Mathf.Clamp(Mathf.RoundToInt(r * 1.2f), 1, 6);
                for (int i = 0; i < bushes; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    Vector3 p = Ground(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r * (float)rng.NextDouble() * 0.55f);
                    NatureKit.Bush(flora, p, 0.9f + r * 0.18f, rng.Next(9999), NatureKit.Green, rng.NextDouble() < 0.3 ? NatureKit.FlowerColors : null);
                }
                if (r > 2.2f && rng.NextDouble() < 0.7)
                    Instance($"Tree_Round_{rng.Next(4)}", parent, Ground(c + new Vector3(0.4f, 0, 0.6f)) + Vector3.down * 0.1f, rng.Next(360), (2.6f + r * 0.3f) / TreeNominalHeight);
            }
            MeshObject("Islet Flora", parent, flora, "Flora", Materials.Foliage, true);
        }

        static void FlushIslandMeshes()
        {
            foreach (var kv in _paths) MeshObject("Paths", Sub(kv.Key, "Ground Detail"), kv.Value, $"Paths_{kv.Key}", Materials.Ground, false);
            foreach (var kv in _flora) MeshObject("Flora", Sub(kv.Key, "Ground Detail"), kv.Value, $"Flora_{kv.Key}", Materials.Foliage, true);
            foreach (var kv in _fences) MeshObject("Fences", Sub(kv.Key, "Ground Detail"), kv.Value, $"Fences_{kv.Key}", Materials.Vertex, true);
        }

        // ------------------------------------------------------------------ camera focus

        static void ComputeFocusPoses()
        {
            var overview = _rig.overview;
            foreach (var kv in _islands)
            {
                var b = new Bounds();
                bool any = false;
                float top = 0f;
                foreach (var def in IslandDefs)
                {
                    if ((def.Owner ?? "") != kv.Key || !IslandShapes.TryGetValue(def.Id, out var shape)) continue;
                    foreach (var r in shape.Rim)
                    {
                        if (!any) { b = new Bounds(r, Vector3.zero); any = true; }
                        else b.Encapsulate(r);
                    }
                    top = Mathf.Max(top, def.Top);
                }
                if (!any) continue;
                float hTan = Mathf.Tan(overview.fov * 0.5f * Mathf.Deg2Rad) * (RefW / RefH);
                float vTan = Mathf.Tan(overview.fov * 0.5f * Mathf.Deg2Rad);
                float distW = b.size.x * 0.62f / hTan;
                float distD = b.size.z * 0.62f * Mathf.Sin(40f * Mathf.Deg2Rad) / vTan;
                var pose = new NumuCameraRig.Pose
                {
                    pivot = new Vector3(b.center.x, top + 1.5f, b.center.z - b.size.z * 0.05f),
                    pitch = 40f,
                    yaw = 0f,
                    distance = Mathf.Max(55f, Mathf.Max(distW, distD) * 1.08f),
                    fov = overview.fov,
                };
                kv.Value.focusPose = pose;
                var fp = new GameObject("Focus Point").transform;
                fp.SetParent(kv.Value.transform, false);
                fp.position = pose.pivot;
                kv.Value.focusPoint = fp;
            }
        }
    }
}
