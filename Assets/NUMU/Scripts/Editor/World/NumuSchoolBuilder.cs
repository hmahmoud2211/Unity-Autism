using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Builds the realistic NUMU School (Assets/Scenes/Worlds/NUMU_School.unity): a calm, small special-needs
    /// classroom (desks, whiteboard, class rules, feelings chart, calm corner) with a teacher, classmates and the
    /// "Class situations" activity (Lina is nervous about reading her story aloud).
    /// </summary>
    public static class NumuSchoolBuilder
    {
        const float X0 = -5f, X1 = 5f, Z0 = -4f, Z1 = 4f, H = 3.0f, T = 0.2f;
        const float IX0 = X0 + T / 2, IX1 = X1 - T / 2, IZ0 = Z0 + T / 2, IZ1 = Z1 - T / 2;
        static readonly Vector3 Inside = new Vector3(0, 1, 0);
        static readonly Vector3 PlayerStart = new Vector3(-4.2f, 0, -3.0f);
        static readonly float[] DeskX = { -2.2f, 0f, 2.2f };
        static readonly float[] DeskZ = { 0.5f, -1.5f };
        const float DeskScale = 0.85f;
        static string TexDir => $"{S.WorldDir}/Textures";

        static NumuWorldScaffold S;

        [MenuItem("NUMU/Worlds/Build School World")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build(bool bake = true)
        {
            S = new NumuWorldScaffold("School");
            try
            {
                NumuWorldIcons.Generate();
                Textures();
                string err = S.Begin();
                if (err != null) return err;
                Shell();
                Front();
                Desks();
                BackAndCalmCorner();
                Lighting();
                People();
                S.SaveAndRegister();
                if (bake) S.Bake(9f);
                S.SaveAndRegister();
                AssetDatabase.SaveAssets();
                S.Log.Insert(0, $"Built {S.ScenePath}\n");
                return S.Log.ToString();
            }
            catch (System.Exception e)
            {
                return $"SCHOOL BUILD FAILED: {e}\n--- log ---\n{S.Log}";
            }
        }

        static Transform G(string name, Transform parent = null) => S.Group(name, parent);

        /// <summary>A flat picture (poster, drawing, board) hung on an inside wall, facing into the room.</summary>
        static GameObject WallPicture(string name, Transform parent, Texture2D tex, Vector3 center, Vector3 intoRoom, Vector2 size, float smoothness = 0.2f, float depth = 0.004f)
        {
            var mb = new MeshBuilder();
            var rot = Quaternion.LookRotation(intoRoom);
            var m = Matrix4x4.TRS(center + intoRoom * depth, rot, Vector3.one);
            var q = new MeshBuilder();
            int a = q.Add(new Vector3(size.x * 0.5f, -size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(0, 0));
            int b = q.Add(new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(1, 0));
            int c = q.Add(new Vector3(-size.x * 0.5f, size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(1, 1));
            int d = q.Add(new Vector3(size.x * 0.5f, size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(0, 1));
            q.QuadFacing(a, b, c, d, Vector3.forward);
            mb.Append(q, m);
            return S.MeshGO(name, parent, mb, NumuInteriorMaterials.Textured($"M_School_{name}", tex, smoothness), true, ShadowCastingMode.Off, true, 2f);
        }

        // ------------------------------------------------------------------ shell

        static readonly WallOpening[] EastWindows = { new WallOpening(0.8f, 1.6f, 0.85f, 2.45f), new WallOpening(3.2f, 1.6f, 0.85f, 2.45f), new WallOpening(5.6f, 1.6f, 0.85f, 2.45f) };
        static readonly WallOpening Door = new WallOpening(0.5f, 1.0f, 0f, 2.15f);

        static void Shell()
        {
            var g = G("Classroom");
            var cream = NumuInteriorMaterials.Paint("M_School_WallCream", NumuColor.Hex("#f2ebdc"));
            var front = NumuInteriorMaterials.Paint("M_School_WallFront", NumuColor.Hex("#dbe6ec"));
            var outside = NumuInteriorMaterials.Acg("M_School_Brick", "Plaster001", 2f, NumuColor.Hex("#e4d6c3"), 0.3f);
            // Calm, low-contrast vinyl planks (a busy speckled floor is a common sensory distraction).
            var floor = NumuInteriorMaterials.Acg("M_School_Floor", "WoodFloor051", 2.4f, NumuColor.Hex("#fbf1e4"), 0.5f, 0.5f);
            var ceiling = NumuInteriorMaterials.Solid("M_School_Ceiling", NumuColor.Hex("#f9f9f6"), 0.1f);
            var skirtingMat = NumuInteriorMaterials.Solid("M_School_Skirting", NumuColor.Hex("#7d8a93"), 0.4f);
            var frameMat = NumuInteriorMaterials.Solid("M_School_WindowFrame", NumuColor.Hex("#eef0f0"), 0.55f);
            var doorMat = NumuInteriorMaterials.Acg("M_School_Door", "Wood049", 1.4f, NumuColor.Hex("#c9a983"), 0.45f);
            var steel = NumuInteriorMaterials.Solid("M_School_Steel", NumuColor.Hex("#b9bec4"), 0.6f, 0.85f);

            var walls = new (Vector3 a, Vector3 b, WallOpening[] o, Material inner)[]
            {
                (new Vector3(X0, 0, Z1), new Vector3(X1, 0, Z1), new WallOpening[0], front),
                (new Vector3(X1, 0, Z0), new Vector3(X1, 0, Z1), EastWindows, cream),
                (new Vector3(X0, 0, Z0), new Vector3(X1, 0, Z0), new WallOpening[0], cream),
                (new Vector3(X0, 0, Z0), new Vector3(X0, 0, Z1), new[] { Door }, cream),
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
                NumuInteriorKit.Skirting(skirting, w.a, w.b, Inside, T, w.o, 0.1f);
                foreach (var o in w.o)
                {
                    if (o.bottom > 0.01f) NumuInteriorKit.Window(frame, glass, sill, w.a, w.b, Inside, o, T);
                    else NumuInteriorKit.Door(casing, leaf, handle, w.a, w.b, Inside, o, T);
                }
                wi++;
            }
            S.MeshGO("WindowFrames", g, frame, frameMat, true, ShadowCastingMode.On, false);
            S.MeshGO("WindowSills", g, sill, frameMat, true, ShadowCastingMode.On, true, 2f);
            var glassGo = S.MeshGO("WindowGlass", g, glass, NumuInteriorMaterials.Glass(), false, ShadowCastingMode.Off, false);
            GameObjectUtility.SetStaticEditorFlags(glassGo, StaticEditorFlags.BatchingStatic);
            // Glass stops the follow camera (it would otherwise slip outside through the window).
            glassGo.AddComponent<MeshCollider>();
            S.MeshGO("Skirting", g, skirting, skirtingMat, true, ShadowCastingMode.Off, false);
            S.MeshGO("DoorCasing", g, casing, frameMat, true, ShadowCastingMode.On, false);
            S.MeshGO("Door", g, leaf, doorMat, true, ShadowCastingMode.On, true, 1f, true);
            S.MeshGO("DoorHandle", g, handle, steel, true, ShadowCastingMode.Off, false);

            var fl = new MeshBuilder();
            NumuInteriorKit.Slab(fl, Rect.MinMaxRect(X0, Z0, X1, Z1), 0f, true);
            S.MeshGO("Floor", g, fl, floor, true, ShadowCastingMode.Off, true, 1f, true);
            var ce = new MeshBuilder();
            NumuInteriorKit.Slab(ce, Rect.MinMaxRect(X0, Z0, X1, Z1), H, false);
            S.MeshGO("Ceiling", g, ce, ceiling, true, ShadowCastingMode.TwoSided, true, 1f);
            var roof = new MeshBuilder();
            NumuInteriorKit.Box(roof, new Vector3(0, H + 0.17f, 0), new Vector3(X1 - X0 + 0.6f, 0.3f, Z1 - Z0 + 0.6f), false);
            S.MeshGO("Roof", g, roof, outside, true, ShadowCastingMode.TwoSided, false);

            // Recessed LED panels (emissive; their light is baked by matching area lights).
            var panels = new MeshBuilder();
            var trims = new MeshBuilder();
            foreach (var p in PanelSpots)
            {
                NumuInteriorKit.Box(trims, new Vector3(p.x, H - 0.01f, p.y), new Vector3(1.24f, 0.02f, 0.64f));
                NumuInteriorKit.Quad(panels, new Vector3(p.x - 0.6f, H - 0.021f, p.y - 0.3f), Vector3.right * 1.2f, Vector3.forward * 0.6f, Vector3.down);
            }
            S.MeshGO("LedPanelTrims", g, trims, frameMat, true, ShadowCastingMode.Off, false);
            S.MeshGO("LedPanels", g, panels, NumuInteriorMaterials.Emissive("M_School_LedPanel", NumuColor.Hex("#f4f7ff"), 1.4f), true, ShadowCastingMode.Off, false);

            var lawn = NumuInteriorMaterials.Acg("M_Lawn", "Grass004", 2.5f, NumuColor.Hex("#c9d8b0"), 0.3f);
            S.Outdoors(Rect.MinMaxRect(X0, Z0, X1, Z1), lawn, 14, 21, 3.5f, 22f);
            S.Log.AppendLine("Shell: 10×8 m classroom, three tall east windows, door, terrazzo floor, LED ceiling panels, schoolyard view.");
        }

        static readonly Vector2[] PanelSpots = { new Vector2(-2.5f, 2.2f), new Vector2(2.5f, 2.2f), new Vector2(-2.5f, -0.6f), new Vector2(2.5f, -0.6f), new Vector2(-2.5f, -3.0f), new Vector2(2.5f, -3.0f) };

        // ------------------------------------------------------------------ the front of the class

        /// <summary>
        /// Paints every texture that contains printed text. This runs before the scene is cleared: URP cannot
        /// render the text camera again in the same Editor tick after the old scene's lights are destroyed.
        /// </summary>
        static void Textures()
        {
            // Whiteboard with the morning message.
            var wb = new NumuSignKit.Canvas(1200, 400, NumuColor.Hex("#f8f9f9"));
            for (int i = 0; i < 9; i++) wb.Ellipse(150 + i * 120, 120 + (i % 3) * 70, 90, 30, new Color(0.8f, 0.82f, 0.84f, 0.05f));
            wb.Text("Good morning, class!", 60, 260, 760, 110, NumuColor.Hex("#2457a8"), 400, false, TextAlignmentOptions.Left);
            wb.Text("Today: Story time", 70, 170, 620, 80, NumuColor.Hex("#2f8a4a"), 400, false, TextAlignmentOptions.Left);
            wb.Text("Who will read first?", 70, 90, 620, 70, NumuColor.Hex("#c0392b"), 400, false, TextAlignmentOptions.Left);
            wb.Line(new Vector2(70, 250), new Vector2(700, 250), 5, NumuColor.Hex("#2457a8"));
            wb.Circle(1000, 250, 70, NumuColor.Hex("#f2a31b"));
            wb.Circle(1000, 250, 58, NumuColor.Hex("#f8f9f9"));
            wb.Circle(1000, 250, 52, NumuColor.Hex("#f7c548"));
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 0.2f;
                wb.Line(new Vector2(1000 + Mathf.Cos(a) * 85, 250 + Mathf.Sin(a) * 85), new Vector2(1000 + Mathf.Cos(a) * 120, 250 + Mathf.Sin(a) * 120), 7, NumuColor.Hex("#f2a31b"));
            }
            wb.Save($"{TexDir}/Whiteboard.png");

            // Alphabet frieze across the top of the front wall.
            var abc = new NumuSignKit.Canvas(2600, 100, NumuColor.Hex("#ffffff"));
            string[] cardColors = { "#f7c9c9", "#fbe3b5", "#c9e8c9", "#c9ddf2", "#e2d4f2" };
            for (int i = 0; i < 26; i++)
            {
                abc.Rect(i * 100 + 4, 4, 92, 92, NumuColor.Hex(cardColors[i % cardColors.Length]), 10);
                char L = (char)('A' + i);
                abc.Text($"{L}{char.ToLower(L)}", i * 100 + 8, 10, 84, 80, NumuColor.Hex("#3d3350"));
            }
            abc.Save($"{TexDir}/AlphabetFrieze.png", 2048);

            // Class rules (left) and numbers (right) beside the board.
            var rules = new NumuSignKit.Canvas(540, 720, NumuColor.Hex("#fff8e7"));
            rules.Rect(0, 0, 540, 720, NumuColor.Hex("#f2b544"), 0);
            rules.Rect(14, 14, 512, 692, NumuColor.Hex("#fff8e7"), 12);
            rules.Text("Our Class Rules", 30, 600, 480, 90, NumuColor.Hex("#8a3a12"));
            string[] rule = { "We listen", "We are kind", "We take turns", "We use quiet voices" };
            string[] ruleColor = { "#3d8ee6", "#e0589c", "#4fae62", "#8c6ad8" };
            for (int i = 0; i < rule.Length; i++)
            {
                int y = 470 - i * 130;
                rules.Circle(80, y + 45, 38, NumuColor.Hex(ruleColor[i]));
                rules.Text((i + 1).ToString(), 50, y + 15, 60, 60, Color.white);
                rules.Text(rule[i], 140, y + 10, 370, 70, NumuColor.Hex("#3d3350"), 400, true, TextAlignmentOptions.Left);
            }
            rules.Save($"{TexDir}/ClassRules.png");

            var nums = new NumuSignKit.Canvas(900, 360, NumuColor.Hex("#ffffff"));
            for (int i = 0; i < 5; i++)
            {
                nums.Rect(i * 180 + 6, 6, 168, 348, NumuColor.Hex(cardColors[i]), 16);
                nums.Text((i + 1).ToString(), i * 180 + 20, 170, 140, 160, NumuColor.Hex("#3d3350"));
                for (int k = 0; k <= i; k++) nums.Circle(i * 180 + 40 + (k % 3) * 45, 120 - (k / 3) * 50, 16, NumuColor.Hex("#3d3350"));
            }
            nums.Save($"{TexDir}/Numbers.png");

            var paper = new NumuSignKit.Canvas(256, 340, NumuColor.Hex("#fbfbf7"));
            for (int i = 0; i < 9; i++) paper.Line(new Vector2(24, 300 - i * 30), new Vector2(232, 300 - i * 30), 2, NumuColor.Hex("#c8d6ea"));
            paper.Text("My story", 24, 290, 200, 44, NumuColor.Hex("#3a3a46"), 400, false, TextAlignmentOptions.Left);
            for (int i = 0; i < 6; i++) paper.Line(new Vector2(28, 250 - i * 30), new Vector2(60 + (i * 53) % 160 + 60, 250 - i * 30), 4, NumuColor.Hex("#5a5f73"));
            paper.Save($"{TexDir}/StoryPaper.png", 512);

            var feel = new NumuSignKit.Canvas(600, 800, NumuColor.Hex("#ffffff"));
            feel.Rect(0, 0, 600, 800, NumuColor.Hex("#3d8ee6"), 0);
            feel.Rect(14, 14, 572, 772, NumuColor.Hex("#ffffff"), 14);
            feel.Text("How do I feel today?", 30, 690, 540, 90, NumuColor.Hex("#1c4fa6"));
            string[] em = { "Happy", "Sad", "Angry", "Fear", "Surprise", "Neutral" };
            string[] label = { "Happy", "Sad", "Angry", "Scared", "Surprised", "Calm" };
            for (int i = 0; i < 6; i++)
            {
                int cx = 50 + (i % 2) * 280, cy = 470 - (i / 2) * 215;
                var icon = NumuSignKit.ReadPng($"{NumuWorldIcons.Dir}/Emotion_{em[i]}.png");
                feel.Image(icon, cx, cy + 50, 150, 150);
                Object.DestroyImmediate(icon);
                feel.Text(label[i], cx - 20, cy, 230, 52, NumuColor.Hex("#3d3350"));
            }
            feel.Save($"{TexDir}/FeelingsChart.png");

            var calm = new NumuSignKit.Canvas(640, 180, NumuColor.Hex("#e9f3ea"));
            calm.Rect(6, 6, 628, 168, NumuColor.Hex("#a8c3a5"), 24);
            calm.Rect(14, 14, 612, 152, NumuColor.Hex("#f4faf3"), 20);
            calm.Text("Calm Corner", 150, 30, 460, 120, NumuColor.Hex("#2f6b3d"));
            calm.Ellipse(85, 92, 40, 58, NumuColor.Hex("#7fb07c"));
            calm.Line(new Vector2(85, 40), new Vector2(85, 140), 5, NumuColor.Hex("#4f8a4c"));
            calm.Save($"{TexDir}/CalmCornerSign.png", 512);
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static void Front()
        {
            var g = G("Front");
            var aluminium = NumuInteriorMaterials.Solid("M_School_Aluminium", NumuColor.Hex("#c7ccd1"), 0.55f, 0.8f);

            var board = WallPicture("Whiteboard", g, Tex("Whiteboard"), new Vector3(0, 1.5f, IZ1), Vector3.back, new Vector2(3.6f, 1.2f), 0.75f, 0.03f);
            var frame = new MeshBuilder();
            NumuInteriorKit.Box(frame, new Vector3(0, 2.115f, IZ1 - 0.02f), new Vector3(3.66f, 0.03f, 0.04f));
            NumuInteriorKit.Box(frame, new Vector3(0, 0.885f, IZ1 - 0.02f), new Vector3(3.66f, 0.03f, 0.04f));
            NumuInteriorKit.Box(frame, new Vector3(-1.815f, 1.5f, IZ1 - 0.02f), new Vector3(0.03f, 1.26f, 0.04f));
            NumuInteriorKit.Box(frame, new Vector3(1.815f, 1.5f, IZ1 - 0.02f), new Vector3(0.03f, 1.26f, 0.04f));
            NumuInteriorKit.Box(frame, new Vector3(0, 0.86f, IZ1 - 0.07f), new Vector3(3.4f, 0.02f, 0.1f));
            S.MeshGO("WhiteboardFrame", g, frame, aluminium, true, ShadowCastingMode.On, false);
            var markers = new MeshBuilder();
            string[] markerColors = { "#2457a8", "#2f8a4a", "#c0392b", "#222222" };
            for (int i = 0; i < markerColors.Length; i++)
            {
                var m = new MeshBuilder();
                Shapes.Cylinder(m, Matrix4x4.TRS(new Vector3(0.9f + i * 0.08f, 0.885f, IZ1 - 0.07f), Quaternion.Euler(0, 0, 90), Vector3.one), 0.009f, 0.009f, 0.13f, 8, Color.white);
                S.MeshGO($"Marker_{i}", g, m, NumuInteriorMaterials.Solid($"M_School_Marker_{i}", NumuColor.Hex(markerColors[i]), 0.5f), true, ShadowCastingMode.Off, false);
            }

            WallPicture("AlphabetFrieze", g, Tex("AlphabetFrieze"), new Vector3(0, 2.62f, IZ1), Vector3.back, new Vector2(7.8f, 0.3f));

            WallPicture("ClassRules", g, Tex("ClassRules"), new Vector3(-3.1f, 1.55f, IZ1), Vector3.back, new Vector2(0.75f, 1.0f));

            WallPicture("Numbers", g, Tex("Numbers"), new Vector3(3.2f, 1.6f, IZ1), Vector3.back, new Vector2(1.5f, 0.6f));

            var clock = S.Place("wall_clock", g, new Vector3(3.2f, 2.22f, IZ1 - 0.03f), 180f);
            if (clock != null)
            {
                var b = NumuWorldScaffold.Bounds(clock);
                clock.transform.position += new Vector3(0, 0, IZ1 - 0.005f - b.max.z);
            }

            // Teacher's desk and chair.
            // Drawers on the teacher's side, plain modesty panel towards the class.
            var desk = S.Place("metal_office_desk", g, new Vector3(-3.0f, 0, 2.75f), 0f, 0.75f, true);
            if (desk != null)
            {
                float top = NumuWorldScaffold.Top(desk);
                S.Place("book_encyclopedia_set_01_row", g, new Vector3(-3.45f, top, 2.95f), 180f, 0.8f);
                S.Place("stationery_supplies", g, new Vector3(-2.55f, top, 2.85f), 160f);
                S.Place("binder_notebook_closed", g, new Vector3(-3.0f, top, 2.6f), 175f);
            }
            S.Place("SchoolChair_01", g, new Vector3(-3.0f, 0, 3.35f), 180f, 1f, true);
            S.Log.AppendLine("Front: whiteboard with the morning message, alphabet frieze, class rules, numbers chart, clock, teacher's desk.");
        }

        // ------------------------------------------------------------------ desks

        static readonly List<(Vector3 desk, Vector3 seat)> Seats = new List<(Vector3, Vector3)>();

        static void Desks()
        {
            var g = G("Desks");
            Seats.Clear();
            var paperMat = NumuInteriorMaterials.Textured("M_School_Paper", Tex("StoryPaper"), 0.1f);
            var sheets = new MeshBuilder();
            int k = 0;
            foreach (float z in DeskZ)
                foreach (float x in DeskX)
                {
                    var deskPos = new Vector3(x, 0, z);
                    var desk = S.Place("SchoolDesk_01", g, deskPos, 0f, DeskScale, true);
                    var chairPos = deskPos + new Vector3(0, 0, -0.5f);
                    S.Place("SchoolChair_01", g, chairPos, 0f, DeskScale, true);
                    Seats.Add((deskPos, chairPos));
                    float top = NumuWorldScaffold.Top(desk);
                    // A story sheet on every desk, plus pencils or a folder on some.
                    var rot = Quaternion.Euler(0, (k * 37 % 17) - 8f, 0);
                    var q = new MeshBuilder();
                    NumuInteriorKit.Quad(q, new Vector3(-0.105f, 0, -0.14f), Vector3.right * 0.21f, Vector3.forward * 0.28f, Vector3.up);
                    for (int v = 0; v < q.UVs.Count; v++) q.UVs[v] = new Vector2(q.UVs[v].x / 0.21f, q.UVs[v].y / 0.28f);
                    sheets.Append(q, Matrix4x4.TRS(new Vector3(x - 0.05f, top + 0.002f, z - 0.02f), rot, Vector3.one));
                    if (k % 2 == 0) S.Place("stationery_supplies", g, new Vector3(x + 0.2f, top, z + 0.1f), k * 40f, 0.9f);
                    else S.Place("binder_notebook_closed", g, new Vector3(x + 0.15f, top, z + 0.05f), 10f + k * 7f, 0.8f);
                    k++;
                }
            S.MeshGO("StorySheets", g, sheets, paperMat, true, ShadowCastingMode.Off, false);
            S.Log.AppendLine($"Desks: {Seats.Count} pupil desks with chairs, story sheets, pencils and folders.");
        }

        // ------------------------------------------------------------------ back wall and calm corner

        static void BackAndCalmCorner()
        {
            var g = G("BackAndCalmCorner");
            // Kids' drawings pinned on a cork board.
            var cork = NumuInteriorMaterials.Acg("M_School_Cork", "Carpet016", 0.6f, NumuColor.Hex("#c99a64"), 0.15f);
            var board = new MeshBuilder();
            NumuInteriorKit.Box(board, new Vector3(1.2f, 1.45f, IZ0 + 0.015f), new Vector3(2.6f, 1.2f, 0.03f));
            S.MeshGO("CorkBoard", g, board, cork, true, ShadowCastingMode.Off, true, 2f);
            for (int i = 0; i < 4; i++)
            {
                var c = new NumuSignKit.Canvas(256, 200, NumuColor.Hex("#fdfdf8"));
                Drawing(c, i);
                WallPicture($"Drawing_{i}", g, c.Save($"{TexDir}/Drawing_{i}.png", 256), new Vector3(0.25f + i * 0.62f, 1.45f + (i % 2 == 0 ? 0.18f : -0.16f), IZ0 + 0.03f), Vector3.forward, new Vector2(0.48f, 0.375f), 0.1f, 0.002f);
            }
            // Supplies shelves with books, baskets and crates.
            var bookMat = BookMaterial();
            foreach (float x in new[] { -3.6f, -2.3f })
            {
                var shelf = S.Place("steel_frame_shelves_01", g, new Vector3(x, 0, IZ0 + 0.3f), 0f, 1f, true);
                if (shelf == null) continue;
                var b = NumuWorldScaffold.Bounds(shelf);
                shelf.transform.position += new Vector3(0, 0, IZ0 + 0.01f - b.min.z);
                b = NumuWorldScaffold.Bounds(shelf);
                var books = new MeshBuilder();
                var rng = new System.Random((int)(x * 10));
                foreach (float y in new[] { 0.42f, 0.9f, 1.38f })
                {
                    float px = b.min.x + 0.06f;
                    while (px < b.max.x - 0.12f)
                    {
                        float w = 0.025f + (float)rng.NextDouble() * 0.03f, h = 0.2f + (float)rng.NextDouble() * 0.09f, d = 0.15f + (float)rng.NextDouble() * 0.05f;
                        if (rng.NextDouble() < 0.12) { px += 0.12f; continue; }
                        int cell = rng.Next(16);
                        Book(books, new Vector3(px + w * 0.5f, y + h * 0.5f, b.center.z), new Vector3(w, h, d), cell, (float)(rng.NextDouble() - 0.5) * 6f);
                        px += w + 0.003f;
                    }
                }
                S.MeshGO("Books", g, books, bookMat, true, ShadowCastingMode.On, false);
                S.Place("wicker_basket_01", g, new Vector3(b.center.x, b.min.y + 1.85f, b.center.z), 0f, 0.9f);
            }

            // Calm corner: soft rug, bean bags, a low book shelf and the feelings chart.
            var rug = new MeshBuilder();
            NumuInteriorKit.Rug(rug, new Vector3(-3.75f, 0, -0.2f), new Vector2(2.1f, 2.6f), 0.3f);
            S.MeshGO("CalmRug", g, rug, NumuInteriorMaterials.Acg("M_School_CalmRug", "Carpet016", 0.8f, NumuColor.Hex("#c9dcec"), 0.15f), true, ShadowCastingMode.Off, true, 2f);
            var fabricN = NumuWorldMaterials.Load<Texture2D>("Assets/ThirdParty/ambientCG/Fabric030/Fabric030_Normal.png");
            string[] bagColors = { "#a8c3a5", "#a9bdd6" };
            for (int i = 0; i < 2; i++)
            {
                var bag = new MeshBuilder();
                Vector3 p = new Vector3(-4.05f + i * 0.55f, 0.24f, -0.9f + i * 1.15f);
                Shapes.Blob(bag, Matrix4x4.TRS(p, Quaternion.Euler(0, i * 50, 0), Vector3.one), new Vector3(0.4f, 0.26f, 0.38f), Color.white, 0.035f, 2.2f, 7 + i);
                Shapes.Blob(bag, Matrix4x4.TRS(p + new Vector3(0, 0.2f, -0.14f), Quaternion.Euler(-20, i * 50, 0), Vector3.one), new Vector3(0.3f, 0.18f, 0.16f), Color.white, 0.03f, 2.4f, 9 + i);
                var go = S.MeshGO($"BeanBag_{i}", g, bag, NumuWorldMaterials.Lit($"M_School_BeanBag_{i}", new NumuWorldMaterials.Spec
                {
                    normal = fabricN, normalScale = 0.7f, color = NumuColor.Hex(bagColors[i]), smoothness = 0.2f, tiling = Vector2.one * 3f,
                }), true, ShadowCastingMode.On, false);
                NumuWorldScaffold.Solid(go, p, new Vector3(0.8f, 0.5f, 0.76f));
            }
            var low = S.Place("wooden_display_shelves_01", g, new Vector3(IX0 + 0.2f, 0, 1.75f), 90f, 1f, true);
            if (low != null)
            {
                var b = NumuWorldScaffold.Bounds(low);
                low.transform.position += new Vector3(IX0 + 0.01f - b.min.x, 0, 0);
                S.Place("potted_plant_04", g, new Vector3(b.center.x + (IX0 + 0.01f - b.min.x), b.max.y, b.center.z + 0.25f), 0f, 1.2f);
            }

            WallPicture("FeelingsChart", g, Tex("FeelingsChart"), new Vector3(IX0, 1.5f, -0.3f), Vector3.right, new Vector2(0.75f, 1.0f));
            WallPicture("CalmCornerSign", g, Tex("CalmCornerSign"), new Vector3(IX0, 2.2f, -0.3f), Vector3.right, new Vector2(1.1f, 0.31f));

            S.Place("standing_chalkboard_01", g, new Vector3(-3.3f, 0, -3.2f), 60f, 0.9f, true);
            int n = 0;
            foreach (var o in EastWindows)
                S.Place("potted_plant_04", g, new Vector3(IX1 - 0.12f, o.bottom, Z0 + o.start + o.width * 0.5f + (n++ - 1) * 0.1f), n * 70f);
            S.Log.AppendLine("Back: cork board with children's drawings, supply shelves with books and baskets. Calm corner: rug, two bean bags, book shelf, feelings chart.");
        }

        static void Drawing(NumuSignKit.Canvas c, int kind)
        {
            Color Crayon(string hex) => NumuColor.Hex(hex);
            switch (kind)
            {
                case 0: // house and sun
                    c.Rect(70, 30, 110, 80, Crayon("#e57373"));
                    c.Line(new Vector2(60, 110), new Vector2(125, 160), 8, Crayon("#8d6e63"));
                    c.Line(new Vector2(125, 160), new Vector2(190, 110), 8, Crayon("#8d6e63"));
                    c.Rect(112, 30, 26, 45, Crayon("#5d4037"));
                    c.Circle(215, 165, 22, Crayon("#ffca28"));
                    c.Line(new Vector2(10, 28), new Vector2(246, 28), 6, Crayon("#66bb6a"));
                    break;
                case 1: // flower
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        c.Circle(128 + Mathf.Cos(a) * 30, 130 + Mathf.Sin(a) * 30, 22, Crayon("#f06292"));
                    }
                    c.Circle(128, 130, 20, Crayon("#ffd54f"));
                    c.Line(new Vector2(128, 105), new Vector2(128, 25), 7, Crayon("#43a047"));
                    c.Ellipse(150, 60, 22, 10, Crayon("#66bb6a"));
                    break;
                case 2: // family
                    for (int i = 0; i < 3; i++)
                    {
                        float x = 70 + i * 60, s = i == 2 ? 0.7f : 1f;
                        c.Circle(x, 60 + 95 * s, 16 * s, Crayon("#ffcc80"));
                        c.Line(new Vector2(x, 60 + 78 * s), new Vector2(x, 60 + 30 * s), 6, Crayon(i == 0 ? "#42a5f5" : i == 1 ? "#ab47bc" : "#ef5350"));
                        c.Line(new Vector2(x, 60 + 30 * s), new Vector2(x - 14, 30), 5, Crayon("#5c6bc0"));
                        c.Line(new Vector2(x, 60 + 30 * s), new Vector2(x + 14, 30), 5, Crayon("#5c6bc0"));
                        c.Line(new Vector2(x - 22, 60 + 62 * s), new Vector2(x + 22, 60 + 62 * s), 5, Crayon("#ffcc80"));
                    }
                    break;
                default: // rainbow
                    string[] r = { "#e53935", "#fb8c00", "#fdd835", "#43a047", "#1e88e5", "#8e24aa" };
                    for (int i = 0; i < r.Length; i++)
                        for (int s = 0; s <= 24; s++)
                        {
                            float a = s / 24f * Mathf.PI;
                            float rad = 100 - i * 11;
                            c.Circle(128 + Mathf.Cos(a) * rad, 30 + Mathf.Sin(a) * rad, 6, Crayon(r[i]));
                        }
                    c.Ellipse(40, 40, 30, 16, Crayon("#eceff1"));
                    c.Ellipse(216, 40, 30, 16, Crayon("#eceff1"));
                    break;
            }
        }

        // ------------------------------------------------------------------ procedural books

        static Material BookMaterial()
        {
            var c = new NumuSignKit.Canvas(512, 512, Color.white);
            string[] cols = { "#c0392b", "#2c5d9e", "#2f8a4a", "#e1a325", "#7d4ea6", "#1f7d86", "#d35400", "#34495e", "#a33a5c", "#5d8a2f", "#9e6b3b", "#3b6e9e", "#d4b03a", "#6c3483", "#b03a2e", "#2e7d5b" };
            for (int i = 0; i < 16; i++)
            {
                int x = (i % 4) * 128, y = (i / 4) * 128;
                c.Rect(x, y, 128, 128, NumuColor.Hex(cols[i]));
                c.Rect(x, y + 18, 128, 8, new Color(1, 1, 1, 0.55f));
                c.Rect(x, y + 102, 128, 8, new Color(1, 1, 1, 0.55f));
                c.Rect(x + 20, y + 50, 88, 22, new Color(1, 1, 1, 0.35f), 4);
            }
            c.Grain(0.08f, 3);
            return NumuInteriorMaterials.Textured("M_School_Books", c.Save($"{TexDir}/BookAtlas.png", 512), 0.3f);
        }

        /// <summary>A book (box) whose faces all map to one cell of the 4×4 book atlas.</summary>
        static void Book(MeshBuilder mb, Vector3 center, Vector3 size, int cell, float tilt)
        {
            var part = new MeshBuilder();
            NumuInteriorKit.Box(part, Vector3.zero, size);
            Vector2 o = new Vector2((cell % 4) * 0.25f, (cell / 4) * 0.25f);
            for (int i = 0; i < part.UVs.Count; i++)
            {
                var uv = part.UVs[i];
                part.UVs[i] = o + new Vector2(Mathf.Clamp01(uv.x / Mathf.Max(size.x, size.z)) * 0.24f + 0.005f, Mathf.Clamp01(uv.y / size.y) * 0.24f + 0.005f);
            }
            mb.Append(part, Matrix4x4.TRS(center, Quaternion.Euler(0, 0, tilt), Vector3.one));
        }

        // ------------------------------------------------------------------ lighting

        static void Lighting()
        {
            // Morning sun through the east windows.
            S.Sun(new Vector3(32f, -100f, 0f), NumuColor.Hex("#fff0dc"), 2.0f);
            S.Sky("Assets/ThirdParty/PolyHaven/HDRI/farm_field_puresky_2k.hdr", 1.0f, 120f, new Color(0.55f, 0.55f, 0.55f));
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = false;
            Color cool = NumuColor.Hex("#f3f6ff"), sky = NumuColor.Hex("#d6e4ff");
            int i = 0;
            foreach (var p in PanelSpots) S.AreaLight($"Panel_{i++}", new Vector3(p.x, H - 0.03f, p.y), new Vector3(90, 0, 0), new Vector2(1.2f, 0.6f), cool, 3.2f);
            i = 0;
            foreach (var o in EastWindows)
                S.AreaLight($"Sky_{i++}", new Vector3(IX1 - 0.12f, (o.bottom + o.top) * 0.5f, Z0 + o.start + o.width * 0.5f), new Vector3(0, -90, 0), new Vector2(o.width - 0.15f, o.top - o.bottom - 0.15f), sky, 1.5f);
            S.Uplight(Rect.MinMaxRect(IX0, IZ0, IX1, IZ1), NumuColor.Hex("#f6f4ee"), 0.9f);
            S.ReflectionProbe("ReflectionProbe", new Vector3(0, H * 0.5f, 0), new Vector3(X1 - X0, H, Z1 - Z0));
            S.LightProbes(Rect.MinMaxRect(IX0 + 0.3f, IZ0 + 0.3f, IX1 - 0.3f, IZ1 - 0.3f), 1.3f, new[] { 0.35f, 1.15f, 2.3f });
            S.PostProcessing(0.05f, 8f, 4f, 2f, 0.18f, 0.16f);
        }

        // ------------------------------------------------------------------ people and the activity

        static void People()
        {
            var g = G("Characters");
            var (player, follow) = S.CreatePlayer(g, PlayerStart, 90f, 3.0f, 1.8f);
            var interactions = S.CreateInteractions(player.transform);

            var teacher = S.SpawnCharacter("Female_Adult_14", "Ms. Sara", g, new Vector3(-1.15f, 0, 2.85f), 160f);
            var teacherNpc = NumuWorldScaffold.MakeNpc(teacher, "Idle", player.transform, "Nod", "LookAround");
            teacher.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.4f);
            NumuWorldScaffold.Blocker(teacher, 0.28f, 1.75f);

            var lina = S.SpawnCharacter("Female_Child_02", "Lina", g, new Vector3(0.95f, 0, 2.7f), 190f);
            lina.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var linaNpc = NumuWorldScaffold.MakeNpc(lina, "Nervous", player.transform);
            lina.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Fear, 0.6f);
            NumuWorldScaffold.Blocker(lina, 0.22f, 1.3f);

            // Classmates at their desks.
            void Pupil(string avatar, string name, int seat, string state, NumuEmotion e, float strength)
            {
                var (desk, chair) = Seats[seat];
                var kid = S.SpawnCharacter(avatar, name, g, chair, 0f);
                NumuWorldScaffold.SeatOn(kid, chair + Vector3.up * 0.39f, avatar.StartsWith("Female") ? "f_sit_table_idle_neutral_01" : "m_sit_table_idle_neutral_01", -0.04f);
                NumuWorldScaffold.MakeNpc(kid, state, lina.transform, "SitTableThink");
                kid.GetComponent<NumuFacialExpressions>().SetExpression(e, strength);
            }
            Pupil("Female_Child_01", "Mia", 1, "SitTable", NumuEmotion.Happy, 0.35f);
            Pupil("Male_Child_02", "Omar", 5, "SitTableRelaxed", NumuEmotion.Neutral, 1f);

            var linaHead = NumuWorldScaffold.Head(lina);
            var interaction = S.Interaction("Interaction_TalkToLina", new Vector3(0.95f, 0, 1.6f), "Talk to Lina", linaHead, 1.6f);

            var go = new GameObject("Scenario_SchoolLinaNervous");
            go.transform.SetParent(S.Root, false);
            var sc = go.AddComponent<NumuConversationScenario>();
            sc.scenarioId = "school_lina_nervous";
            sc.world = "school";
            sc.progressIsland = "school";
            sc.progressLevel = 4;
            sc.introToast = "It's story time! Lina is at the front of the class. Go and see her.";
            sc.doneToast = "Lina feels braver now. Look around the classroom, or tap Map to go back.";
            sc.actors.Add(new NumuConversationScenario.Actor { name = "Lina", npc = linaNpc, face = lina.GetComponent<NumuFacialExpressions>(), faceAnchor = linaHead, turnToPlayer = true, shotDistance = 1.1f });
            sc.actors.Add(new NumuConversationScenario.Actor { name = "Ms. Sara", npc = teacherNpc, face = teacher.GetComponent<NumuFacialExpressions>(), faceAnchor = NumuWorldScaffold.Head(teacher), turnToPlayer = true, shotDistance = 1.4f });
            sc.interactable = interaction;
            sc.player = player;
            sc.followCamera = follow;
            sc.interactions = interactions;

            var L = NumuConversationScenario.StepKind.Line;
            var E = NumuConversationScenario.StepKind.Expression;
            var A = NumuConversationScenario.StepKind.Animation;
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 1, text = "Lina, it's your turn to read your story to the class." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 0, emotion = NumuEmotion.Fear, strength = 0.95f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "TalkNervous" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Um... I don't know if I can. Everyone will be looking at me." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Nervous" });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.EmotionQuestion, actor = 0, text = "How is Lina feeling?", emotion = NumuEmotion.Fear,
                praise = "Lina feels scared, or nervous. Her eyes are wide open and her eyebrows go up.",
                hint = "Look at Lina's eyes and eyebrows: they are wide and raised. She is worried about reading in front of everyone.",
            });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.ChoiceQuestion, actor = 0, text = "What could you say to Lina?",
                options = new List<NumuConversationScenario.Option>
                {
                    NumuHomeBuilder.Opt("You can do it! I'll listen.", "Choice_ThumbsUp", "#dff3e3"),
                    NumuHomeBuilder.Opt("Ha ha, you're scared!", "Choice_Laugh", "#fff1b8"),
                    NumuHomeBuilder.Opt("Hurry up!", "Choice_Shout", "#ffdcd6"),
                    NumuHomeBuilder.Opt("Say nothing", "Choice_Ignore", "#e8e6f0"),
                },
                correctOption = 0,
                praiseTitle = "What a good friend!",
                praise = "Kind words help someone feel brave. Lina knows you are on her side.",
                hint = "Laughing or rushing her would make Lina feel worse. What could you say to help her feel brave?",
            });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = -1, text = "You can do it, Lina! I'll listen." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 0, emotion = NumuEmotion.Happy, strength = 0.8f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Idle" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Thank you! I feel braver now." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 1, emotion = NumuEmotion.Happy, strength = 0.8f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 1, text = "What a kind friend you are! Let's all listen to Lina's story." });

            sc.ui = NumuWorldUIBuilder.Build(S.Root, new NumuWorldUIBuilder.Spec
            {
                canvasName = "School UI",
                title = "NUMU School",
                subtitle = "Class situations",
                chipIcon = NumuTextureKit.Sprite("Icon_School"),
                chipBg = NumuColor.Hex("#e4f1fd"),
                chipInk = NumuColor.Hex("#1c4fa6"),
                accent = NumuColor.Hex("#3d8ee6"),
                prompt = "Talk to Lina",
                speaker = "Lina",
                question = "How is Lina feeling?",
            }, interactions, player);
            S.Log.AppendLine("People: the child, Ms. Sara (teacher), Lina (nervous, at the front), Mia and Omar at their desks. Activity school_lina_nervous: 2 questions.");
        }
    }
}
