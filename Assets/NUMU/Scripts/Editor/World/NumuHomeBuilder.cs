using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Builds the realistic NUMU Home (Assets/Scenes/Worlds/NUMU_Home.unity): an open-plan family living room,
    /// dining area and kitchen with CC0 furniture, PBR surfaces, baked daylight, Mum and Dad, and the
    /// "Recognize family emotions" activity (Mum is sad about her broken cup).
    /// </summary>
    public static class NumuHomeBuilder
    {
        // House shell (wall centre lines), metres.
        const float X0 = -6.5f, X1 = 6.5f, Z0 = -4.5f, Z1 = 4.5f, H = 2.8f, T = 0.2f;
        const float IX0 = X0 + T / 2, IX1 = X1 - T / 2, IZ0 = Z0 + T / 2, IZ1 = Z1 - T / 2;
        static readonly Vector3 Inside = new Vector3(0, 1, 0);
        static readonly Vector3 PlayerStart = new Vector3(-1.1f, 0, -3.6f);
        static readonly Vector3 TablePos = new Vector3(2.6f, 0, -1.0f);
        static readonly Vector3 SofaPos = new Vector3(-3.3f, 0, 0.3f);

        static NumuWorldScaffold S;

        [MenuItem("NUMU/Worlds/Build Home World")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build(bool bake = true)
        {
            S = new NumuWorldScaffold("Home");
            try
            {
                string err = S.Begin();
                if (err != null) return err;
                NumuWorldIcons.Generate();
                Shell();
                Kitchen();
                Living();
                Dining();
                Lighting();
                var scenario = People();
                S.SaveAndRegister();
                if (bake) S.Bake(9f);
                S.SaveAndRegister();
                AssetDatabase.SaveAssets();
                S.Log.Insert(0, $"Built {S.ScenePath}\n");
                return S.Log.ToString();
            }
            catch (System.Exception e)
            {
                return $"HOME BUILD FAILED: {e}\n--- log ---\n{S.Log}";
            }
        }

        // ------------------------------------------------------------------ helpers

        static Transform G(string name, Transform parent = null) => S.Group(name, parent);

        /// <summary>Slides a placed object so its back touches an inside wall face (walls are axis aligned).</summary>
        static GameObject Snap(GameObject go, Vector3 intoRoom, float gap = 0.01f)
        {
            if (go == null) return null;
            var b = NumuWorldScaffold.Bounds(go);
            Vector3 d = Vector3.zero;
            if (intoRoom.z < -0.5f) d.z = IZ1 - gap - b.max.z;
            if (intoRoom.z > 0.5f) d.z = IZ0 + gap - b.min.z;
            if (intoRoom.x < -0.5f) d.x = IX1 - gap - b.max.x;
            if (intoRoom.x > 0.5f) d.x = IX0 + gap - b.min.x;
            go.transform.position += d;
            return go;
        }

        static GameObject On(string id, Transform parent, GameObject surface, Vector2 xz, float yaw, float scale = 1f) =>
            S.Place(id, parent, new Vector3(xz.x, NumuWorldScaffold.Top(surface), xz.y), yaw, scale);

        // ------------------------------------------------------------------ shell

        static readonly WallOpening SouthWindowA = new WallOpening(1.1f, 2.0f, 0.9f, 2.2f);
        static readonly WallOpening FrontDoor = new WallOpening(4.9f, 1.0f, 0f, 2.1f);
        static readonly WallOpening SouthWindowB = new WallOpening(8.7f, 2.2f, 0.9f, 2.2f);
        static readonly WallOpening WestWindow = new WallOpening(2.9f, 3.2f, 0.45f, 2.3f);
        static readonly WallOpening KitchenWindow = new WallOpening(9.5f, 1.6f, 1.15f, 2.2f);
        static readonly WallOpening HallDoor = new WallOpening(5.6f, 1.0f, 0f, 2.1f);
        static readonly WallOpening EastWindow = new WallOpening(1.7f, 1.8f, 0.9f, 2.2f);

        static void Shell()
        {
            var g = G("House");
            var wallPaint = NumuInteriorMaterials.Paint("M_Home_WallPaint", NumuColor.Hex("#efe6d8"));
            var accent = NumuInteriorMaterials.Paint("M_Home_AccentWall", NumuColor.Hex("#a9bfae"));
            var outside = NumuInteriorMaterials.Acg("M_Home_Render", "Plaster001", 2f, NumuColor.Hex("#e9dfcf"), 0.3f);
            var wood = NumuInteriorMaterials.Acg("M_Home_OakFloor", "WoodFloor051", 2.2f, NumuColor.Hex("#f6e7d6"), 0.55f);
            var tiles = NumuInteriorMaterials.Acg("M_Home_KitchenTiles", "Tiles107", 1.2f, NumuColor.Hex("#ece9e4"), 0.6f);
            var ceiling = NumuInteriorMaterials.Solid("M_Home_Ceiling", NumuColor.Hex("#fbfaf6"), 0.1f);
            var trim = NumuInteriorMaterials.Solid("M_Home_WhiteTrim", NumuColor.Hex("#f3f1ec"), 0.45f);
            var frameMat = NumuInteriorMaterials.Solid("M_Home_WindowFrame", NumuColor.Hex("#f5f5f2"), 0.5f);
            var doorWood = NumuInteriorMaterials.Acg("M_Home_DoorOak", "Wood049", 1.4f, NumuColor.Hex("#d8c0a2"), 0.45f);
            var metal = NumuWorldMaterials.Lit("M_Home_BrushedSteel", new NumuWorldMaterials.Spec
            {
                baseMap = NumuWorldMaterials.Load<Texture2D>("Assets/ThirdParty/ambientCG/Metal032/Metal032_Color.png"),
                normal = NumuWorldMaterials.Load<Texture2D>("Assets/ThirdParty/ambientCG/Metal032/Metal032_Normal.png"),
                metallic = 0.9f, smoothness = 0.62f, tiling = Vector2.one / 0.8f,
            });

            var walls = new (Vector3 a, Vector3 b, WallOpening[] o, Material inner)[]
            {
                (new Vector3(X0, 0, Z0), new Vector3(X1, 0, Z0), new[] { SouthWindowA, FrontDoor, SouthWindowB }, wallPaint),
                (new Vector3(X0, 0, Z0), new Vector3(X0, 0, Z1), new[] { WestWindow }, wallPaint),
                (new Vector3(X0, 0, Z1), new Vector3(X1, 0, Z1), new[] { KitchenWindow, HallDoor }, wallPaint),
                (new Vector3(X1, 0, Z0), new Vector3(X1, 0, Z1), new[] { EastWindow }, accent),
            };
            var frame = new MeshBuilder();
            var glass = new MeshBuilder();
            var sill = new MeshBuilder();
            var skirting = new MeshBuilder();
            var casing = new MeshBuilder();
            var leaf = new MeshBuilder();
            var handle = new MeshBuilder();
            int wi = 0;
            foreach (var w in walls)
            {
                var inner = new MeshBuilder();
                var outer = new MeshBuilder();
                NumuInteriorKit.Wall(inner, outer, w.a, w.b, Inside, H, T, w.o);
                S.MeshGO($"Wall_{wi}_Inside", g, inner, w.inner, true, ShadowCastingMode.TwoSided, true, 1f, true);
                S.MeshGO($"Wall_{wi}_Outside", g, outer, outside, true, ShadowCastingMode.On, true, 0.3f);
                NumuInteriorKit.Skirting(skirting, w.a, w.b, Inside, T, w.o);
                foreach (var o in w.o)
                {
                    if (o.bottom > 0.01f) NumuInteriorKit.Window(frame, glass, sill, w.a, w.b, Inside, o, T);
                    else NumuInteriorKit.Door(casing, leaf, handle, w.a, w.b, Inside, o, T);
                }
                wi++;
            }
            S.MeshGO("WindowFrames", g, frame, frameMat, true, ShadowCastingMode.On, false);
            S.MeshGO("WindowSills", g, sill, trim, true, ShadowCastingMode.On, true, 2f);
            var glassGo = S.MeshGO("WindowGlass", g, glass, NumuInteriorMaterials.Glass(), false, ShadowCastingMode.Off, false);
            GameObjectUtility.SetStaticEditorFlags(glassGo, StaticEditorFlags.BatchingStatic);
            // Glass stops the follow camera (it would otherwise slip outside through the window).
            glassGo.AddComponent<MeshCollider>();
            S.MeshGO("Skirting", g, skirting, trim, true, ShadowCastingMode.Off, false);
            S.MeshGO("DoorCasings", g, casing, trim, true, ShadowCastingMode.On, false);
            S.MeshGO("Doors", g, leaf, doorWood, true, ShadowCastingMode.On, true, 1f, true);
            S.MeshGO("DoorHandles", g, handle, metal, true, ShadowCastingMode.Off, false);

            // Floors: oak boards everywhere except the tiled kitchen strip.
            var woodFloor = new MeshBuilder();
            NumuInteriorKit.Slab(woodFloor, Rect.MinMaxRect(X0, Z0, 1.0f, Z1), 0f, true);
            NumuInteriorKit.Slab(woodFloor, Rect.MinMaxRect(1.0f, Z0, X1, 2.5f), 0f, true);
            S.MeshGO("Floor_Oak", g, woodFloor, wood, true, ShadowCastingMode.Off, true, 1f, true);
            var tileFloor = new MeshBuilder();
            NumuInteriorKit.Slab(tileFloor, Rect.MinMaxRect(1.0f, 2.5f, X1, Z1), 0f, true);
            S.MeshGO("Floor_KitchenTiles", g, tileFloor, tiles, true, ShadowCastingMode.Off, true, 1f, true);
            var ceil = new MeshBuilder();
            NumuInteriorKit.Slab(ceil, Rect.MinMaxRect(X0, Z0, X1, Z1), H, false);
            S.MeshGO("Ceiling", g, ceil, ceiling, true, ShadowCastingMode.TwoSided, true, 1f);
            // Roof slab so the sun can't leak in from above. No bottom face and lifted 2 cm: coplanar with the
            // ceiling it would z-fight (a blotchy patch of facade texture across the ceiling).
            var roof = new MeshBuilder();
            NumuInteriorKit.Box(roof, new Vector3(0, H + 0.17f, 0), new Vector3(X1 - X0 + 0.6f, 0.3f, Z1 - Z0 + 0.6f), false);
            S.MeshGO("Roof", g, roof, outside, true, ShadowCastingMode.TwoSided, false);

            // Curtains and rods beside the living-room and dining windows.
            var curtains = new MeshBuilder();
            var rods = new MeshBuilder();
            void CurtainPair(Vector3 a, Vector3 b, WallOpening o)
            {
                Vector3 dir = (b - a).normalized;
                Vector3 n = Vector3.Cross(Vector3.up, dir).normalized;
                if (Vector3.Dot(Inside - a, n) < 0f) n = -n;
                var rot = Quaternion.LookRotation(n);
                float top = Mathf.Min(H - 0.08f, o.top + 0.3f);
                foreach (float s in new[] { -1f, 1f })
                {
                    float along = s < 0 ? o.start - 0.3f : o.End + 0.3f;
                    Vector3 c = a + dir * along + n * (T * 0.5f + 0.12f) + Vector3.up * top;
                    NumuInteriorKit.Curtain(curtains, Matrix4x4.TRS(c, rot, Vector3.one), 0.62f, top - 0.04f, 0.045f, 5.5f);
                }
                Vector3 r0 = a + dir * (o.start - 0.7f) + n * (T * 0.5f + 0.12f) + Vector3.up * (top + 0.02f);
                Vector3 r1 = a + dir * (o.End + 0.7f) + n * (T * 0.5f + 0.12f) + Vector3.up * (top + 0.02f);
                Shapes.Beam(rods, r0, r1, 0.012f, 8, Color.white);
            }
            CurtainPair(walls[0].a, walls[0].b, SouthWindowA);
            CurtainPair(walls[0].a, walls[0].b, SouthWindowB);
            CurtainPair(walls[1].a, walls[1].b, WestWindow);
            // Linen curtains: woven normal detail over a light, warm colour.
            var curtainMat = NumuWorldMaterials.Lit("M_Home_CurtainsDS", new NumuWorldMaterials.Spec
            {
                normal = NumuWorldMaterials.Load<Texture2D>("Assets/ThirdParty/ambientCG/Fabric030/Fabric030_Normal.png"), normalScale = 0.6f,
                color = NumuColor.Hex("#e8dcc6"), smoothness = 0.18f, tiling = Vector2.one / 0.6f, doubleSided = true,
            });
            S.MeshGO("Curtains", g, curtains, curtainMat, true, ShadowCastingMode.On, false);
            S.MeshGO("CurtainRods", g, rods, metal, true, ShadowCastingMode.Off, false);

            var lawn = NumuInteriorMaterials.Acg("M_Lawn", "Grass004", 2.5f, NumuColor.Hex("#c9d8b0"), 0.3f);
            S.Outdoors(Rect.MinMaxRect(X0, Z0, X1, Z1), lawn, 16, 7, 4f, 24f);
            S.Log.AppendLine("Shell: 13×9 m open-plan ground floor, 7 openings (5 windows with glass and curtains, 2 doors), oak and tile floors, garden view.");
        }

        // ------------------------------------------------------------------ kitchen

        static void Kitchen()
        {
            var g = G("Kitchen");
            var cabinetPaint = NumuInteriorMaterials.Solid("M_Home_CabinetSage", NumuColor.Hex("#8fa595"), 0.4f);
            var carcass = NumuInteriorMaterials.Solid("M_Home_CabinetCarcass", NumuColor.Hex("#e9e4da"), 0.35f);
            var marble = NumuInteriorMaterials.Acg("M_Home_Worktop", "Marble012", 1.2f, Color.white, 0.85f);
            var tiles = NumuInteriorMaterials.Acg("M_Home_Splashback", "Tiles107", 0.9f, NumuColor.Hex("#f4f2ee"), 0.75f);
            var steel = AssetDatabase.LoadAssetAtPath<Material>($"{NumuWorldMaterials.Dir}/M_Home_BrushedSteel.mat");
            var north = Quaternion.Euler(0, 180, 0);

            var body = new MeshBuilder();
            var doors = new MeshBuilder();
            var handles = new MeshBuilder();
            // Base runs face the room (-z); in their frame +x runs towards -x, so each starts at its right-hand end.
            NumuInteriorKit.CabinetRun(body, doors, handles, Matrix4x4.TRS(new Vector3(1.8f, 0, IZ1), north, Vector3.one), 0.6f, 0.9f, 0.58f, 0.6f, true);
            NumuInteriorKit.CabinetRun(body, doors, handles, Matrix4x4.TRS(new Vector3(5.5f, 0, IZ1), north, Vector3.one), 3.1f, 0.9f, 0.58f, 0.52f, true);
            NumuInteriorKit.CabinetRun(body, doors, handles, Matrix4x4.TRS(new Vector3(2.9f, 1.48f, IZ1), north, Vector3.one), 1.7f, 0.7f, 0.34f, 0.56f, false);
            NumuInteriorKit.CabinetRun(body, doors, handles, Matrix4x4.TRS(new Vector3(5.5f, 1.48f, IZ1), north, Vector3.one), 0.8f, 0.7f, 0.34f, 0.4f, false);
            var bodyGo = S.MeshGO("Cabinets", g, body, carcass, true, ShadowCastingMode.On, true, 2f);
            NumuWorldScaffold.Solid(bodyGo, new Vector3(3.35f, 0.45f, IZ1 - 0.3f), new Vector3(4.3f, 0.9f, 0.62f));
            S.MeshGO("CabinetDoors", g, doors, cabinetPaint, true, ShadowCastingMode.On, true, 2f);
            S.MeshGO("CabinetHandles", g, handles, steel, true, ShadowCastingMode.Off, false);

            var top = new MeshBuilder();
            var sinkHole = Rect.MinMaxRect(3.45f, IZ1 - 0.5f, 4.15f, IZ1 - 0.1f);
            NumuInteriorKit.Worktop(top, Rect.MinMaxRect(1.2f, IZ1 - 0.62f, 1.8f, IZ1), 0.94f, 0.04f);
            NumuInteriorKit.Worktop(top, Rect.MinMaxRect(2.4f, IZ1 - 0.62f, 5.5f, IZ1), 0.94f, 0.04f, new[] { sinkHole });
            S.MeshGO("Worktop", g, top, marble, true, ShadowCastingMode.On, true, 3f);

            // Stainless sink basin and a swan-neck tap.
            var sink = new MeshBuilder();
            float by = 0.74f;
            NumuInteriorKit.Quad(sink, new Vector3(sinkHole.xMin, by, sinkHole.yMin), Vector3.forward * sinkHole.height, Vector3.right * sinkHole.width, Vector3.up);
            NumuInteriorKit.Quad(sink, new Vector3(sinkHole.xMin, by, sinkHole.yMin), Vector3.right * sinkHole.width, Vector3.up * (0.94f - by), Vector3.forward);
            NumuInteriorKit.Quad(sink, new Vector3(sinkHole.xMax, by, sinkHole.yMax), Vector3.left * sinkHole.width, Vector3.up * (0.94f - by), Vector3.back);
            NumuInteriorKit.Quad(sink, new Vector3(sinkHole.xMin, by, sinkHole.yMax), Vector3.back * sinkHole.height, Vector3.up * (0.94f - by), Vector3.right);
            NumuInteriorKit.Quad(sink, new Vector3(sinkHole.xMax, by, sinkHole.yMin), Vector3.forward * sinkHole.height, Vector3.up * (0.94f - by), Vector3.left);
            var tap = new List<Vector3> { new Vector3(3.8f, 0.94f, IZ1 - 0.06f), new Vector3(3.8f, 1.2f, IZ1 - 0.06f), new Vector3(3.8f, 1.3f, IZ1 - 0.16f), new Vector3(3.8f, 1.22f, IZ1 - 0.28f) };
            Shapes.Tube(sink, Shapes.SampleSpline(tap, 0.02f), 0.012f, 10, Color.white);
            S.MeshGO("Sink", g, sink, steel, true, ShadowCastingMode.On, false);

            var splash = new MeshBuilder();
            float z = IZ1 - 0.004f;
            void Splash(float x0, float x1, float y0, float y1) => NumuInteriorKit.Quad(splash, new Vector3(x1, y0, z), Vector3.left * (x1 - x0), Vector3.up * (y1 - y0), Vector3.back, new Vector2(x0, y0));
            Splash(1.2f, 3.0f, 0.94f, 1.48f);
            Splash(3.0f, 4.6f, 0.94f, 1.15f);
            Splash(4.6f, 5.5f, 0.94f, 1.48f);
            S.MeshGO("Splashback", g, splash, tiles, true, ShadowCastingMode.Off, true, 3f);

            var fridgeBody = new MeshBuilder();
            var fridgeHandles = new MeshBuilder();
            NumuInteriorKit.Fridge(fridgeBody, fridgeHandles, Matrix4x4.TRS(new Vector3(5.95f, 0, IZ1), north, Vector3.one), 0.76f, 1.86f, 0.66f);
            var fridge = S.MeshGO("Fridge", g, fridgeBody, steel, true, ShadowCastingMode.On, true, 2f);
            NumuWorldScaffold.Solid(fridge, new Vector3(5.95f, 0.93f, IZ1 - 0.35f), new Vector3(0.78f, 1.86f, 0.7f));
            S.MeshGO("FridgeHandles", g, fridgeHandles, NumuInteriorMaterials.Solid("M_Home_BlackHandles", NumuColor.Hex("#2a2a2c"), 0.6f), true, ShadowCastingMode.Off, false);

            Snap(S.Place("electric_stove", g, new Vector3(2.1f, 0, IZ1 - 0.3f), 180f, 1f, true), Vector3.back);
            var counter = new GameObject("CounterSurface");
            counter.transform.SetParent(g, false);
            counter.transform.position = new Vector3(0, 0.94f, 0);
            Place("vintage_electric_kettle", g, new Vector3(2.75f, 0.94f, IZ1 - 0.28f), 200f);
            Place("wooden_cutting_board", g, new Vector3(5.05f, 0.94f, IZ1 - 0.3f), 170f);
            var bowl = Place("wooden_bowl_01", g, new Vector3(4.6f, 0.94f, IZ1 - 0.32f), 0f);
            float bowlTop = bowl != null ? NumuWorldScaffold.Bounds(bowl).min.y + 0.05f : 0.99f;
            var rng = new System.Random(3);
            for (int i = 0; i < 4; i++)
                Place(i < 3 ? "food_apple_01" : "lemon", g, new Vector3(4.6f + (float)(rng.NextDouble() - 0.5) * 0.12f, bowlTop, IZ1 - 0.32f + (float)(rng.NextDouble() - 0.5) * 0.12f), rng.Next(360));
            Object.DestroyImmediate(counter);
            S.Log.AppendLine("Kitchen: sage shaker cabinets, marble worktop with an undermount sink, tiled splashback, fridge-freezer, stove, kettle, fruit bowl.");
        }

        static GameObject Place(string id, Transform parent, Vector3 pos, float yaw, float scale = 1f, bool collider = false) => S.Place(id, parent, pos, yaw, scale, collider);

        // ------------------------------------------------------------------ living room

        static void Living()
        {
            var g = G("LivingRoom");
            var rug = new MeshBuilder();
            NumuInteriorKit.Rug(rug, new Vector3(-3.3f, 0, 1.5f), new Vector2(3.4f, 2.5f));
            S.MeshGO("Rug", g, rug, NumuInteriorMaterials.Acg("M_Home_Rug", "Carpet016", 0.8f, NumuColor.Hex("#e4d9c8"), 0.15f), true, ShadowCastingMode.Off, true, 2f);

            S.Place("Sofa_01", g, SofaPos, 0f, 1f, true);
            S.Place("throw_pillows_01", g, SofaPos + new Vector3(0.45f, 0.42f, -0.12f), 0f, 0.8f);
            S.Place("coffee_table_round_01", g, new Vector3(-3.3f, 0, 1.75f), 0f, 1f, true);
            S.Place("modern_arm_chair_01", g, new Vector3(-1.05f, 0, 1.3f), -115f, 1f, true);

            var cabinet = Snap(S.Place("modern_wooden_cabinet", g, new Vector3(-3.3f, 0, IZ1 - 0.3f), 180f, 0.8f, true), Vector3.back);
            // Flat-screen TV on the media cabinet.
            float y = NumuWorldScaffold.Top(cabinet);
            var tv = new MeshBuilder();
            var screen = new MeshBuilder();
            Vector3 c = new Vector3(-3.3f, y, NumuWorldScaffold.Bounds(cabinet).center.z);
            NumuInteriorKit.Box(tv, c + new Vector3(0, 0.012f, 0), new Vector3(0.36f, 0.024f, 0.22f));
            NumuInteriorKit.Box(tv, c + new Vector3(0, 0.07f, 0.02f), new Vector3(0.06f, 0.1f, 0.03f));
            NumuInteriorKit.Box(tv, c + new Vector3(0, 0.46f, 0.02f), new Vector3(1.24f, 0.72f, 0.035f));
            NumuInteriorKit.Quad(screen, c + new Vector3(0.6f, 0.12f, -0.0f), Vector3.left * 1.2f, Vector3.up * 0.68f, Vector3.back);
            S.MeshGO("TV", g, tv, NumuInteriorMaterials.Solid("M_Home_TVBody", NumuColor.Hex("#1b1b1d"), 0.55f), true, ShadowCastingMode.On, false);
            S.MeshGO("TVScreen", g, screen, NumuInteriorMaterials.Solid("M_Home_TVScreen", NumuColor.Hex("#0b0c0f"), 0.92f), true, ShadowCastingMode.Off, false);
            On("standing_picture_frame_01", g, cabinet, new Vector2(-4.25f, c.z), 160f);
            On("ceramic_vase_01", g, cabinet, new Vector2(-2.3f, c.z), 0f);

            var shelves = Snap(S.Place("wooden_display_shelves_01", g, new Vector3(IX0 + 0.25f, 0, -3.1f), 90f, 1f, true), Vector3.right);
            if (shelves != null)
            {
                var b = NumuWorldScaffold.Bounds(shelves);
                S.Place("book_encyclopedia_set_01_row", g, new Vector3(b.center.x, b.max.y, b.center.z + 0.25f), 90f, 0.6f);
                S.Place("rubber_duck_toy", g, new Vector3(b.center.x, b.max.y, b.center.z - 0.35f), 120f, 0.6f);
            }
            S.Place("football_ball", g, new Vector3(-5.2f, 0, -2.1f), 30f);
            S.Place("chess_set", g, new Vector3(-3.15f, 0.42f, 1.75f), 20f, 0.55f);
            // One large plant only: the scanned plants are dense meshes (70k+ triangles).
            Snap(S.Place("potted_plant_02", g, new Vector3(IX0 + 0.4f, 0, 3.85f), 0f), Vector3.right);

            // Wooden toy blocks left on the rug ("Organize toys").
            var colors = new[] { "#d9534f", "#3f7fc9", "#f0b429", "#4caf6a" };
            var rng = new System.Random(11);
            for (int k = 0; k < colors.Length; k++)
            {
                var blocks = new MeshBuilder();
                for (int i = 0; i < 3; i++)
                {
                    var p = new Vector3(-4.6f + (float)rng.NextDouble() * 1.1f, 0.012f + 0.035f, 2.3f + (float)rng.NextDouble() * 0.5f);
                    Shapes.RoundedBox(blocks, Matrix4x4.TRS(p, Quaternion.Euler(0, rng.Next(90), 0), Vector3.one), Vector3.one * 0.07f, 0.006f, 1, Color.white);
                }
                S.MeshGO($"ToyBlocks_{k}", g, blocks, NumuInteriorMaterials.Solid($"M_Home_ToyBlock_{k}", NumuColor.Hex(colors[k]), 0.45f), true, ShadowCastingMode.On, false);
            }

            // The frame's picture side faces -Z in the model.
            var frame = S.Place("hanging_picture_frame_02", g, new Vector3(-2.4f, 1.35f, IZ0 + 0.02f), 180f);
            Snap(frame, Vector3.forward);
            var clock = S.Place("wall_clock", g, new Vector3(IX1 - 0.03f, 1.95f, 1.0f), -90f);
            Snap(clock, Vector3.left);
            var lamp = S.Place("modern_ceiling_lamp_01", g, new Vector3(-3.3f, 0, 1.6f), 0f);
            if (lamp != null) lamp.transform.position += Vector3.up * (H - NumuWorldScaffold.Bounds(lamp).max.y);
            S.Log.AppendLine("Living room: sofa with cushions, armchair, round coffee table, media cabinet with a flat-screen TV, toy shelves, scattered toys, plants, artwork, rug.");
        }

        // ------------------------------------------------------------------ dining

        static GameObject _table;
        static readonly (Vector3 pos, float yaw)[] Chairs =
        {
            (TablePos + new Vector3(0.72f, 0, 0), 270f),   // Mum's chair, facing the living room
            (TablePos + new Vector3(0, 0, 0.72f), 180f),
            (TablePos + new Vector3(0, 0, -0.72f), 0f),
            (TablePos + new Vector3(-0.95f, 0, -0.55f), 65f), // pulled out
        };

        static void Dining()
        {
            var g = G("Dining");
            // The scanned table is bar height (1.0 m); 0.75 scale gives a 76 cm, 1.05 m dining table.
            _table = S.Place("round_wooden_table_01", g, TablePos, 0f, 0.75f, true);
            for (int i = 0; i < Chairs.Length; i++) S.Place("dining_chair_02", g, Chairs[i].pos, Chairs[i].yaw, 1f, true);
            float top = NumuWorldScaffold.Top(_table);
            S.Place("tea_set_01_teapot", g, new Vector3(TablePos.x - 0.12f, top, TablePos.z + 0.22f), 200f);
            S.Place("tea_set_01_cup", g, new Vector3(TablePos.x - 0.05f, top, TablePos.z - 0.3f), 30f);

            // Mum's favourite cup, broken in two with a few chips, on its saucer.
            var cup = new MeshBuilder();
            Vector3 c = new Vector3(TablePos.x + 0.26f, top, TablePos.z - 0.04f);
            var saucer = new List<Vector2> { new Vector2(0, 0), new Vector2(0.05f, 0), new Vector2(0.075f, 0.012f), new Vector2(0.078f, 0.016f), new Vector2(0.06f, 0.012f), new Vector2(0, 0.008f) };
            Shapes.Lathe(cup, Matrix4x4.Translate(c), saucer, 28, Color.white);
            var wall = new List<Vector2> { new Vector2(0, 0), new Vector2(0.026f, 0), new Vector2(0.036f, 0.02f), new Vector2(0.042f, 0.065f), new Vector2(0.038f, 0.065f), new Vector2(0.032f, 0.022f), new Vector2(0.022f, 0.006f), new Vector2(0, 0.006f) };
            Shapes.Lathe(cup, Matrix4x4.TRS(c + new Vector3(-0.012f, 0.016f, 0.004f), Quaternion.Euler(0, 20, 0), Vector3.one), wall, 18, Color.white, true, 190f);
            Shapes.Lathe(cup, Matrix4x4.TRS(c + new Vector3(0.05f, 0.03f, -0.06f), Quaternion.Euler(80, 140, 0), Vector3.one), wall, 18, Color.white, true, 150f);
            Shapes.Torus(cup, Matrix4x4.TRS(c + new Vector3(-0.09f, 0.008f, 0.05f), Quaternion.Euler(90, 30, 0), Vector3.one), 0.016f, 0.004f, 12, 6, Color.white, 220f);
            var rng = new System.Random(5);
            for (int i = 0; i < 5; i++)
                Shapes.RoundedBox(cup, Matrix4x4.TRS(c + new Vector3((float)rng.NextDouble() * 0.18f - 0.12f, 0.003f, (float)rng.NextDouble() * 0.16f - 0.1f), Quaternion.Euler(0, rng.Next(180), 0), Vector3.one),
                    new Vector3(0.012f + (float)rng.NextDouble() * 0.01f, 0.004f, 0.008f), 0.0015f, 1, Color.white);
            S.MeshGO("BrokenCup", g, cup, NumuInteriorMaterials.Solid("M_Home_Porcelain", NumuColor.Hex("#f4efe6"), 0.85f), true, ShadowCastingMode.On, false);

            var sideboard = Snap(S.Place("drawer_cabinet", g, new Vector3(IX1 - 0.3f, 0, 1.0f), -90f, 1f, true), Vector3.left);
            if (sideboard != null)
            {
                var b = NumuWorldScaffold.Bounds(sideboard);
                S.Place("potted_plant_04", g, new Vector3(b.center.x, b.max.y, b.center.z - 0.3f), 0f);
            }
            var lamp = S.Place("modern_ceiling_lamp_01", g, TablePos, 0f);
            if (lamp != null) lamp.transform.position += Vector3.up * (H - NumuWorldScaffold.Bounds(lamp).max.y);
            S.Log.AppendLine("Dining: round table, four chairs (one pulled out), tea set, the broken cup, sideboard with plant and picture, pendant lamp.");
        }

        // ------------------------------------------------------------------ lighting

        static void Lighting()
        {
            // Late-afternoon sun low through the west window.
            S.Sun(new Vector3(24f, 74f, 0f), NumuColor.Hex("#ffdcb4"), 2.3f);
            S.Sky("Assets/ThirdParty/PolyHaven/HDRI/farm_field_puresky_2k.hdr", 1.0f, 210f, new Color(0.55f, 0.55f, 0.55f));
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = false;

            Color warm = NumuColor.Hex("#ffe1c0"), sky = NumuColor.Hex("#d6e4ff");
            var down = new Vector3(90, 0, 0);
            S.AreaLight("Ceiling_Living", new Vector3(-3.3f, H - 0.02f, 1.4f), down, new Vector2(1.4f, 1.4f), warm, 3.4f);
            S.AreaLight("Ceiling_Dining", new Vector3(TablePos.x, H - 0.02f, TablePos.z), down, new Vector2(1.1f, 1.1f), warm, 3.2f);
            S.AreaLight("Ceiling_Kitchen", new Vector3(3.6f, H - 0.02f, 3.4f), down, new Vector2(2.2f, 0.6f), warm, 3.2f);
            S.AreaLight("Ceiling_Entry", new Vector3(-1.1f, H - 0.02f, -3.2f), down, new Vector2(0.8f, 0.8f), warm, 2.6f);
            // Skylight through each window, so bounce light looks like daylight rather than a dark box.
            S.AreaLight("Sky_West", new Vector3(IX0 + 0.12f, 1.4f, 0f), new Vector3(0, 90, 0), new Vector2(3.0f, 1.7f), sky, 2.6f);
            S.AreaLight("Sky_SouthA", new Vector3(-4.4f, 1.55f, IZ0 + 0.12f), Vector3.zero, new Vector2(1.8f, 1.1f), sky, 2.1f);
            S.AreaLight("Sky_SouthB", new Vector3(3.3f, 1.55f, IZ0 + 0.12f), Vector3.zero, new Vector2(2.0f, 1.1f), sky, 2.1f);
            S.AreaLight("Sky_East", new Vector3(IX1 - 0.12f, 1.55f, -1.9f), new Vector3(0, -90, 0), new Vector2(1.6f, 1.1f), sky, 1.9f);
            S.AreaLight("Sky_Kitchen", new Vector3(3.8f, 1.67f, IZ1 - 0.12f), new Vector3(0, 180, 0), new Vector2(1.4f, 0.9f), sky, 1.8f);

            S.Uplight(Rect.MinMaxRect(IX0, IZ0, IX1, IZ1), NumuColor.Hex("#fff2e2"), 0.9f);
            S.ReflectionProbe("ReflectionProbe", new Vector3(0, H * 0.5f, 0), new Vector3(X1 - X0, H, Z1 - Z0));
            S.LightProbes(Rect.MinMaxRect(IX0 + 0.3f, IZ0 + 0.3f, IX1 - 0.3f, IZ1 - 0.3f), 1.4f, new[] { 0.35f, 1.15f, 2.2f });
            S.PostProcessing(0.05f, 10f, 6f, 5f, 0.2f, 0.18f);
        }

        // ------------------------------------------------------------------ people and the activity

        static NumuConversationScenario People()
        {
            var g = G("Characters");
            var (player, follow) = S.CreatePlayer(g, PlayerStart, 0f, 3.1f, 1.75f);
            var interactions = S.CreateInteractions(player.transform);

            var mumChair = Chairs[0];
            var mum = S.SpawnCharacter("Female_Adult_06", "Mum", g, mumChair.pos, mumChair.yaw);
            mum.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            NumuWorldScaffold.SeatOn(mum, mumChair.pos + Vector3.up * 0.47f, "f_sit_table_idle_neutral_01", -0.02f);
            var mumNpc = NumuWorldScaffold.MakeNpc(mum, "SitTable", player.transform);
            mumNpc.maxLookWeight = 0.45f;
            mum.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Sad, 0.85f);

            var dad = S.SpawnCharacter("Male_Adult_08", "Dad", g, SofaPos, 0f);
            NumuWorldScaffold.SeatOn(dad, SofaPos + new Vector3(-0.5f, 0.44f, 0.05f), "m_sit_chair_idle_neutral_01", 0.06f);
            NumuWorldScaffold.MakeNpc(dad, "Sit", player.transform);
            dad.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.3f);

            var head = NumuWorldScaffold.Head(mum);
            var interaction = S.Interaction("Interaction_TalkToMum", TablePos + new Vector3(-1.35f, 0, 0.05f), "Talk to Mum", head, 1.9f);

            var go = new GameObject("Scenario_HomeMumSad");
            go.transform.SetParent(S.Root, false);
            var sc = go.AddComponent<NumuConversationScenario>();
            sc.scenarioId = "home_mum_sad";
            sc.world = "home";
            sc.progressIsland = "home";
            sc.progressLevel = 4;
            sc.introToast = "Mum is at the dining table. Go and talk to her.";
            sc.doneToast = "Mum feels better now. Keep exploring your home, or tap Map to go back.";
            sc.actors.Add(new NumuConversationScenario.Actor { name = "Mum", npc = mumNpc, face = mum.GetComponent<NumuFacialExpressions>(), faceAnchor = head, turnToPlayer = false, shotDistance = 1.2f });
            sc.interactable = interaction;
            sc.player = player;
            sc.followCamera = follow;
            sc.interactions = interactions;

            var K = NumuConversationScenario.StepKind.Line;
            sc.steps.Add(new NumuConversationScenario.Step { kind = NumuConversationScenario.StepKind.Expression, actor = 0, emotion = NumuEmotion.Sad, strength = 1f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = K, actor = 0, text = "Oh... hello, sweetheart." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = K, actor = 0, text = "My favourite cup fell and broke. Grandma gave it to me." });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.EmotionQuestion, actor = 0, text = "How is Mum feeling?", emotion = NumuEmotion.Sad,
                praise = "Mum is sad. The middle of her eyebrows goes up and the corners of her mouth go down.",
                hint = "Let's look again. Mum's mouth turns down and she is looking at her broken cup. Is that a happy face?",
            });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.ChoiceQuestion, actor = 0, text = "Mum is sad. What could you do?",
                options = new List<NumuConversationScenario.Option>
                {
                    Opt("Give Mum a hug", "Choice_Hug", "#ffe1ea"),
                    Opt("Laugh at the cup", "Choice_Laugh", "#fff1b8"),
                    Opt("Walk away", "Choice_WalkAway", "#e8e6f0"),
                    Opt("Shout", "Choice_Shout", "#ffdcd6"),
                },
                correctOption = 0,
                praiseTitle = "That's so kind!",
                praise = "A hug shows Mum that you care. Kindness helps people feel better when they are sad.",
                hint = "Laughing, walking away or shouting won't help Mum feel better. What would show her that you care?",
            });
            sc.steps.Add(new NumuConversationScenario.Step { kind = K, actor = -1, text = "Are you okay, Mum? Here's a big hug!" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = NumuConversationScenario.StepKind.Expression, actor = 0, emotion = NumuEmotion.Happy, strength = 0.85f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = NumuConversationScenario.StepKind.Animation, actor = 0, state = "SitTableRelaxed" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = K, actor = 0, text = "Thank you, sweetheart. Your hug makes me feel much better!" });

            var ui = NumuWorldUIBuilder.Build(S.Root, new NumuWorldUIBuilder.Spec
            {
                canvasName = "Home UI",
                title = "NUMU Home",
                subtitle = "Recognize family emotions",
                chipIcon = NumuTextureKit.Sprite("Icon_Home"),
                chipBg = NumuColor.Hex("#ecf7dc"),
                chipInk = NumuColor.Hex("#1f6a34"),
                accent = NumuColor.Hex("#4fae62"),
                prompt = "Talk to Mum",
                speaker = "Mum",
                question = "How is Mum feeling?",
            }, interactions, player);
            sc.ui = ui;
            S.Log.AppendLine("People: the child (third person), Mum at the table (sad, broken cup), Dad on the sofa. Activity home_mum_sad: 2 questions (emotion + kind response).");
            return sc;
        }

        public static NumuConversationScenario.Option Opt(string label, string icon, string color) =>
            new NumuConversationScenario.Option { label = label, icon = NumuWorldIcons.Sprite(icon), color = NumuColor.Hex(color) };
    }
}
