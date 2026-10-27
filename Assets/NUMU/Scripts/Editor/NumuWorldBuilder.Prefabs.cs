using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Numu.EditorTools
{
    /// <summary>Reusable prefabs (vegetation, mascots, buildings, props, level markers) saved under Assets/NUMU/Prefabs.</summary>
    public static partial class NumuWorldBuilder
    {
        public const float TreeNominalHeight = 4f;

        static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, float> _prefabHeights = new Dictionary<string, float>();
        static readonly Dictionary<string, float> _prefabWidths = new Dictionary<string, float>();
        static TMP_FontAsset _font;
        static Material _signTextMat, _markerTextMat, _markerFaceMat;

        public static GameObject Prefab(string name) => _prefabs.TryGetValue(name, out var p) ? p : null;

        static GameObject SavePrefab(GameObject go)
        {
            string path = $"{PrefabsDir}/{go.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            _prefabs[prefab.name] = prefab;
            return prefab;
        }

        static GameObject Part(Transform parent, string name, MeshBuilder mb, Material mat, string group, bool shadows = true)
        {
            if (mb.VertexCount == 0) return null;
            return MeshObject(name, parent, mb, group, mat, shadows);
        }

        static void RecordBounds(string name, GameObject root)
        {
            var b = new Bounds(root.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponent<TMP_Text>() != null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            _prefabHeights[name] = b.max.y;
            _prefabWidths[name] = b.size.x;
        }

        static GameObject MeshPrefab(string name, string group, params (string part, MeshBuilder mb, Material mat)[] parts)
        {
            var root = new GameObject(name);
            foreach (var p in parts) Part(root.transform, p.part, p.mb, p.mat, group);
            RecordBounds(name, root);
            return SavePrefab(root);
        }

        // ------------------------------------------------------------------ text materials

        static void EnsureTextMaterials()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (_font == null) throw new System.Exception("TextMesh Pro essentials are missing (LiberationSans SDF).");
            _signTextMat = TextPreset("TMP_SignText", NumuColor.Hex("#ffffff"), NumuColor.Hex("#5a2a12"), 0.22f, 0.18f, NumuColor.Hex("#4a200c"));
            _markerTextMat = TextPreset("TMP_MarkerText", NumuColor.Hex("#ffffff"), NumuColor.Hex("#00000000"), 0.0f, 0.2f, new Color(0, 0, 0, 0.25f));
            _markerFaceMat = Materials.Make("M_MarkerFace", "NUMU/Toon", m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_VertexColorStrength", 0f);
                m.SetColor("_ShadeColor", NumuColor.Hex("#d6cbe6"));
                m.SetFloat("_ShadeSoftness", 0.5f);
                m.SetFloat("_ShadowStrength", 0.6f);
                m.SetFloat("_RimStrength", 0.1f);
                m.SetFloat("_HighlightStrength", 0.35f);
                m.SetFloat("_HighlightSize", 0.97f);
                m.SetFloat("_WindStrength", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Cull", 2f);
            });
        }

        public static Material TextPreset(string name, Color face, Color outline, float outlineWidth, float dilate, Color underlay)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(_font.material) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = _font.material.shader;
            m.SetTexture("_MainTex", _font.atlasTexture);
            m.SetColor("_FaceColor", face);
            m.SetColor("_OutlineColor", outline);
            m.SetFloat("_OutlineWidth", outlineWidth);
            m.SetFloat("_FaceDilate", dilate);
            if (underlay.a > 0f)
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", underlay);
                m.SetFloat("_UnderlayOffsetX", 0.35f);
                m.SetFloat("_UnderlayOffsetY", -0.45f);
                m.SetFloat("_UnderlayDilate", 0.15f);
                m.SetFloat("_UnderlaySoftness", 0.2f);
            }
            else m.DisableKeyword("UNDERLAY_ON");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ build all prefabs

        static void BuildPrefabs()
        {
            _prefabs.Clear();
            EnsureTextMaterials();
            const string g = "Prefabs";

            // Vegetation.
            for (int i = 0; i < 4; i++) { var mb = new MeshBuilder(); NatureKit.RoundTree(mb, Vector3.zero, TreeNominalHeight, 100 + i); MeshPrefab($"Tree_Round_{i}", g, ("Tree", mb, Materials.Foliage)); }
            for (int i = 0; i < 2; i++) { var mb = new MeshBuilder(); NatureKit.RoundTree(mb, Vector3.zero, TreeNominalHeight, 200 + i, NatureKit.YellowGreen); MeshPrefab($"Tree_Yellow_{i}", g, ("Tree", mb, Materials.Foliage)); }
            for (int i = 0; i < 3; i++) { var mb = new MeshBuilder(); NatureKit.PineTree(mb, Vector3.zero, TreeNominalHeight, 300 + i); MeshPrefab($"Tree_Pine_{i}", g, ("Tree", mb, Materials.Foliage)); }
            for (int i = 0; i < 3; i++) { var mb = new MeshBuilder(); NatureKit.BlossomTree(mb, Vector3.zero, TreeNominalHeight, 400 + i); MeshPrefab($"Tree_Blossom_{i}", g, ("Tree", mb, Materials.Foliage)); }
            for (int i = 0; i < 4; i++) { var mb = new MeshBuilder(); NatureKit.Bush(mb, Vector3.zero, 1f, 500 + i, null, i % 2 == 0 ? NatureKit.FlowerColors : null); MeshPrefab($"Bush_{i}", g, ("Bush", mb, Materials.Foliage)); }
            {
                var mb = new MeshBuilder();
                IslandKit.Boulder(mb, Vector3.zero, new Vector3(1.6f, 2.6f, 1.4f), 77, 0f, NumuColor.Hex("#a9a6b4"));
                Shapes.Blob(mb, Matrix4x4.Translate(new Vector3(0, 1.95f, 0)), new Vector3(1.05f, 0.55f, 0.95f), Color.white, 0.12f, 1.6f, 3, 2, 0.5f);
                MeshPrefab("Rock_SnowCap", g, ("Rock", mb, Materials.Rock));
            }

            // Mascots.
            MascotPrefab("Mascot_Fox", CharacterKit.Fox, "home");
            MascotPrefab("Mascot_Penguin", CharacterKit.Penguin, "school");
            MascotPrefab("Mascot_Bunny", CharacterKit.Bunny, "park");
            MascotPrefab("Mascot_Bear", CharacterKit.Bear, "supermarket");

            // Buildings.
            { var p = new BuildingParts(); ArchitectureKit.HomeCottage(p, 11); BuildingPrefab("Building_HomeCottage", p, null); }
            { var p = new BuildingParts(); ArchitectureKit.SmallHouse(p, 12); BuildingPrefab("Building_SmallHouse", p, null); }
            { var p = new BuildingParts(); var flag = new MeshBuilder(); ArchitectureKit.School(p, flag, 21); BuildingPrefab("Building_School", p, flag); }
            { var p = new BuildingParts(); var flag = new MeshBuilder(); ArchitectureKit.HubCottage(p, flag, 51); BuildingPrefab("Building_HubCottage", p, flag); }
            {
                var p = new BuildingParts();
                ArchitectureKit.Supermarket(p, 41, out var decalPos, out var decalSize);
                var go = BuildingRoot("Building_Supermarket", p, null);
                Decal(go.transform, "CartSign", decalPos, decalSize, NumuTextureKit.Texture("Icon_Cart_White"), "M_Decal_Cart");
                RecordBounds(go.name, go);
                SavePrefab(go);
            }

            // Props.
            Simple("Prop_Playground", mb => PropKit.Playground(mb, 61));
            Simple("Prop_Swing", mb => PropKit.SwingSet(mb, 62));
            Simple("Prop_Bench", mb => PropKit.Bench(mb, 63));
            Simple("Prop_Mailbox", PropKit.Mailbox);
            Simple("Prop_Parasol", mb => PropKit.ParasolTable(mb, 64));
            Simple("Prop_Blackboard", mb => PropKit.Blackboard(mb, 65));
            Simple("Prop_Desk", mb => PropKit.DeskWithBooks(mb, 66));
            Simple("Prop_ProduceStand", mb => PropKit.ProduceStand(mb, 67));
            Simple("Prop_Cart", mb => PropKit.ShoppingCart(mb, 68));
            Simple("Prop_Crates", mb => PropKit.CrateStack(mb, 69));
            Simple("Prop_Stall", mb => PropKit.StripedStall(mb, 70));
            Simple("Prop_ChalkSign", mb => PropKit.ChalkSign(mb, 71));
            Simple("Prop_Shelf", mb => PropKit.ShelfOfGoods(mb, 72));
            Simple("Prop_Gazebo", mb => ArchitectureKit.Gazebo(mb, 73, NumuColor.Hex("#ef7f9c")));
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                Simple($"Prop_FruitCrate_{i}", mb => PropKit.ProduceCrate(mb, PropKit.Fruit[k], 80 + k));
            }
            Simple("Prop_Planter", mb => PropKit.Planter(mb, 2.4f, 1.1f, 74));
            {
                var mb = new MeshBuilder(); var glow = new MeshBuilder();
                PropKit.LampPost(mb, glow);
                MeshPrefab("Prop_Lamp", g, ("Post", mb, Materials.Vertex), ("Glow", glow, Materials.Lamp));
            }
            {
                var mb = new MeshBuilder(); var water = new MeshBuilder();
                PropKit.Fountain(mb, water, 75);
                MeshPrefab("Prop_Fountain", g, ("Stone", mb, Materials.Vertex), ("Water", water, Materials.Glass));
            }
            {
                var mb = new MeshBuilder(); var glass = new MeshBuilder();
                PropKit.SchoolBus(mb, glass);
                MeshPrefab("Prop_SchoolBus", g, ("Body", mb, Materials.Glossy), ("Windows", glass, Materials.Glass));
            }
            {
                var mb = new MeshBuilder(); var sail = new MeshBuilder();
                PropKit.Sailboat(mb, sail, 76);
                var go = new GameObject("Prop_Sailboat");
                Part(go.transform, "Hull", mb, Materials.Vertex, g);
                Part(go.transform, "Sail", sail, Materials.Flag, g);
                go.AddComponent<NumuDrift>().amplitude = new Vector3(0.05f, 0.08f, 0.05f);
                go.GetComponent<NumuDrift>().rollDegrees = 3f;
                go.GetComponent<NumuDrift>().speed = 0.9f;
                RecordBounds(go.name, go);
                SavePrefab(go);
            }

            LevelMarkerPrefab();
            _log.AppendLine($"Prefabs: {_prefabs.Count} saved to {PrefabsDir}");
        }

        static void Simple(string name, System.Action<MeshBuilder> build)
        {
            var mb = new MeshBuilder();
            build(mb);
            MeshPrefab(name, "Prefabs", ("Mesh", mb, Materials.Vertex));
        }

        static GameObject BuildingRoot(string name, BuildingParts p, MeshBuilder flag)
        {
            var root = new GameObject(name);
            Part(root.transform, "Body", p.Main, Materials.Vertex, "Prefabs");
            Part(root.transform, "Glass", p.Glass, Materials.Glass, "Prefabs");
            Part(root.transform, "LitWindows", p.Lit, Materials.WindowLit, "Prefabs");
            if (flag != null) Part(root.transform, "Flag", flag, Materials.Flag, "Prefabs");
            return root;
        }

        static void BuildingPrefab(string name, BuildingParts p, MeshBuilder flag)
        {
            var root = BuildingRoot(name, p, flag);
            RecordBounds(name, root);
            SavePrefab(root);
        }

        public static GameObject Decal(Transform parent, string name, Vector3 pos, Vector2 size, Texture2D tex, string matName)
        {
            var mb = new MeshBuilder();
            // Seen from the camera (looking down +Z), +X is screen-right, so u grows with x.
            int a = mb.Add(new Vector3(-size.x / 2, -size.y / 2, 0), Vector3.back, Color.white, new Vector2(0, 0));
            int b = mb.Add(new Vector3(size.x / 2, -size.y / 2, 0), Vector3.back, Color.white, new Vector2(1, 0));
            int c = mb.Add(new Vector3(size.x / 2, size.y / 2, 0), Vector3.back, Color.white, new Vector2(1, 1));
            int d = mb.Add(new Vector3(-size.x / 2, size.y / 2, 0), Vector3.back, Color.white, new Vector2(0, 1));
            mb.QuadFacing(a, b, c, d, Vector3.back);
            var mat = Materials.Textured(matName, tex, 0.4f, true);
            var go = MeshObject(name, parent, mb, "Prefabs", mat, false);
            go.transform.localPosition = pos;
            return go;
        }

        static void MascotPrefab(string name, System.Action<MeshBuilder, MeshBuilder> build, string islandId)
        {
            var body = new MeshBuilder();
            var eyes = new MeshBuilder();
            build(body, eyes);
            var root = new GameObject(name);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(visual, "Body", body, Materials.Character, "Prefabs");
            Part(visual, "Eyes", eyes, Materials.CharacterEyes, "Prefabs");
            var drift = visual.gameObject.AddComponent<NumuDrift>();
            drift.amplitude = new Vector3(0f, 0.06f, 0f);
            drift.speed = 1.6f;
            drift.yawDegrees = 6f;
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.6f, 0);
            col.height = 3.4f;
            col.radius = 1.0f;
            root.AddComponent<NumuIslandLink>().islandId = islandId;
            RecordBounds(name, root);
            SavePrefab(root);
        }

        static void LevelMarkerPrefab()
        {
            var root = new GameObject("LevelMarker");
            var marker = root.AddComponent<NumuLevelMarker>();
            var col = root.AddComponent<SphereCollider>();
            col.center = new Vector3(0, 0.3f, 0);
            col.radius = 1.25f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var rim = new MeshBuilder();
            var face = new MeshBuilder();
            PropKit.MarkerBase(rim, face, 1f);
            Part(visual, "Rim", rim, Materials.Vertex, "Prefabs");
            var faceGo = Part(visual, "Face", face, _markerFaceMat, "Prefabs");
            var lockMb = new MeshBuilder();
            PropKit.LockIcon(lockMb, 0.9f);
            var lockGo = Part(visual, "Lock", lockMb, Materials.Vertex, "Prefabs");
            lockGo.transform.localPosition = new Vector3(0.74f, 0.5f, 0.52f);
            lockGo.transform.localScale = Vector3.one * 0.6f;
            var starMb = new MeshBuilder();
            PropKit.StarIcon(starMb, 0.75f);
            var starGo = Part(visual, "Star", starMb, Materials.Glossy, "Prefabs");
            starGo.transform.localPosition = new Vector3(0.72f, 0.5f, 0.55f);

            var textGo = new GameObject("Number");
            textGo.transform.SetParent(visual, false);
            textGo.transform.localPosition = new Vector3(0, 0.38f, 0);
            textGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = _font;
            tmp.fontSharedMaterial = _markerTextMat;
            tmp.text = "1";
            tmp.fontSize = 11f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(1.6f, 1.6f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            marker.face = faceGo.GetComponent<Renderer>();
            marker.number = tmp;
            marker.lockIcon = lockGo;
            marker.starIcon = starGo;
            marker.visual = visual;
            SavePrefab(root);
        }

        public static GameObject Instance(string prefabName, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var prefab = Prefab(prefabName);
            if (prefab == null) throw new System.Exception($"Prefab '{prefabName}' was not built");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static float PrefabHeight(string name) => _prefabHeights.TryGetValue(name, out var h) ? h : 1f;
        public static float PrefabWidth(string name) => _prefabWidths.TryGetValue(name, out var w) ? w : 1f;
    }
}
