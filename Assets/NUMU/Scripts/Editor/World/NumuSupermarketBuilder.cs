using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Numu.World;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Builds the realistic NUMU Supermarket (Assets/Scenes/Worlds/NUMU_Supermarket.unity): glass storefront,
    /// stocked aisles, chillers, a produce section, checkouts, shoppers and the "Wait in line / Talk to cashier"
    /// activity (an impatient shopper in the queue, then greeting the cashier).
    /// </summary>
    public static class NumuSupermarketBuilder
    {
        const float X0 = -12f, X1 = 12f, Z0 = -9f, Z1 = 9f, H = 4.2f, T = 0.25f;
        const float IX0 = X0 + T / 2, IX1 = X1 - T / 2, IZ0 = Z0 + T / 2, IZ1 = Z1 - T / 2;
        static readonly Vector3 Inside = new Vector3(0, 1, 0);
        static readonly Vector3 PlayerStart = new Vector3(-1.8f, 0, -7.6f);
        static readonly float[] AisleX = { -6.5f, -2.5f, 1.5f, 5.5f };
        const float GondolaZ0 = -1.2f, GondolaZ1 = 6.3f;
        const float LaneX = 3.0f, CounterX = 3.75f;
        static string TexDir => $"{S.WorldDir}/Textures";

        static NumuWorldScaffold S;
        static Material _products, _produce;

        [MenuItem("NUMU/Worlds/Build Supermarket World")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build(bool bake = true)
        {
            S = new NumuWorldScaffold("Supermarket");
            try
            {
                NumuWorldIcons.Generate();
                // Everything with printed text is painted before the scene is cleared (see Textures()).
                Textures();
                _products = NumuWorldMaterials.Lit("M_Market_Products", new NumuWorldMaterials.Spec { baseMap = Tex("ProductAtlas"), smoothness = 0.45f });
                _produce = NumuWorldMaterials.Lit("M_Market_Produce", new NumuWorldMaterials.Spec { baseMap = NumuProductKit.BuildProducePalette($"{TexDir}/ProducePalette.png"), smoothness = 0.55f });
                string err = S.Begin();
                if (err != null) return err;
                Shell();
                Aisles();
                Chillers();
                Produce();
                Checkouts();
                Signs();
                Lighting();
                People();
                S.SaveAndRegister();
                if (bake) S.Bake(5f);
                S.SaveAndRegister();
                AssetDatabase.SaveAssets();
                S.Log.Insert(0, $"Built {S.ScenePath}\n");
                return S.Log.ToString();
            }
            catch (System.Exception e)
            {
                return $"SUPERMARKET BUILD FAILED: {e}\n--- log ---\n{S.Log}";
            }
        }

        static Transform G(string name, Transform parent = null) => S.Group(name, parent);

        static GameObject TexturedQuad(string name, Transform parent, Material mat, Vector3 center, Vector3 facing, Vector2 size, bool twoSided)
        {
            var mb = new MeshBuilder();
            var rot = Quaternion.LookRotation(facing);
            foreach (float side in twoSided ? new[] { 1f, -1f } : new[] { 1f })
            {
                var q = new MeshBuilder();
                int a = q.Add(new Vector3(size.x * 0.5f, -size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(0, 0));
                int b = q.Add(new Vector3(-size.x * 0.5f, -size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(1, 0));
                int c = q.Add(new Vector3(-size.x * 0.5f, size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(1, 1));
                int d = q.Add(new Vector3(size.x * 0.5f, size.y * 0.5f, 0), Vector3.forward, Color.white, new Vector2(0, 1));
                q.QuadFacing(a, b, c, d, Vector3.forward);
                mb.Append(q, Matrix4x4.TRS(center + facing.normalized * 0.006f * side, rot * Quaternion.Euler(0, side > 0 ? 0 : 180, 0), Vector3.one));
            }
            return S.MeshGO(name, parent, mb, mat, true, ShadowCastingMode.Off, false);
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static void SignTexture(string name, string text, Color bg, Color ink, int w = 1024, int h = 256)
        {
            var c = new NumuSignKit.Canvas(w, h, bg);
            c.Rect(10, 10, w - 20, h - 20, NumuColor.Shade(bg, 1.12f), 18);
            c.Text(text, 40, 30, w - 80, h - 60, ink);
            c.Save($"{TexDir}/{name}.png", 1024);
        }

        static Material SignMaterial(string name) => NumuInteriorMaterials.Textured($"M_Market_{name}", Tex(name), 0.3f);

        static readonly string[] AisleNames = { "Breakfast & Cereal", "Pasta, Rice & Cans", "Snacks & Sauces", "Drinks & Household" };
        static readonly string[] AisleColors = { "#f39b2c", "#3d8ee6", "#e0589c", "#4fae62" };
        static readonly (string name, string text, string color)[] HangingSigns =
        {
            ("Sign_Produce", "Fruit & Vegetables", "#3f9b4f"), ("Sign_Dairy", "Dairy & Drinks", "#2f7fc1"),
            ("Sign_Bakery", "Bakery", "#c8562a"), ("Sign_Checkout", "Checkout", "#f39b2c"),
        };

        /// <summary>
        /// Paints every texture that contains printed text. This runs before the scene is cleared: URP cannot
        /// render the text camera again in the same Editor tick after the old scene's lights are destroyed.
        /// </summary>
        static void Textures()
        {
            NumuProductKit.BuildAtlas($"{TexDir}/ProductAtlas.png");
            for (int i = 0; i < AisleNames.Length; i++) SignTexture($"Aisle_{i}", $"{i + 1}  {AisleNames[i]}", NumuColor.Hex(AisleColors[i]), Color.white);
            foreach (var (name, text, color) in HangingSigns) SignTexture(name, text, NumuColor.Hex(color), Color.white);
            foreach (bool open in new[] { true, false })
            {
                var lane = new NumuSignKit.Canvas(256, 256, open ? NumuColor.Hex("#2f9e57") : NumuColor.Hex("#8a8f96"));
                lane.Circle(128, 150, 80, Color.white);
                lane.Text(open ? "1" : "2", 70, 95, 116, 110, open ? NumuColor.Hex("#2f9e57") : NumuColor.Hex("#8a8f96"));
                lane.Text(open ? "OPEN" : "CLOSED", 20, 12, 216, 50, Color.white);
                lane.Save($"{TexDir}/Lane{(open ? 1 : 2)}.png", 256);
            }
            var welcome = new NumuSignKit.Canvas(1024, 256, NumuColor.Hex("#f39b2c"));
            welcome.Rect(12, 12, 1000, 232, NumuColor.Hex("#fff5c9"), 24);
            welcome.Text("Welcome to NUMU Market", 60, 60, 900, 140, NumuColor.Hex("#8a3a12"));
            welcome.Save($"{TexDir}/Welcome.png", 1024);
            var strip = new NumuSignKit.Canvas(512, 32, NumuColor.Hex("#ffffff"));
            string[] prices = { "1.99", "2.49", "0.89", "3.75" };
            for (int i = 0; i < 4; i++)
            {
                strip.Rect(i * 128 + 8, 3, 64, 26, NumuColor.Hex("#f6d23a"), 3);
                strip.Text(prices[i], i * 128 + 10, 4, 60, 24, NumuColor.Hex("#222222"));
                strip.Rect(i * 128 + 80, 10, 36, 3, NumuColor.Hex("#999999"));
                strip.Rect(i * 128 + 80, 18, 28, 3, NumuColor.Hex("#999999"));
            }
            var tex = strip.Save($"{TexDir}/PriceStrip.png", 512);
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }

        // ------------------------------------------------------------------ shell

        static readonly WallOpening[] Front =
        {
            new WallOpening(0.8f, 2.6f, 0.35f, 3.2f), new WallOpening(3.6f, 2.4f, 0.35f, 3.2f), new WallOpening(6.2f, 2.4f, 0.35f, 3.2f),
            new WallOpening(9.0f, 2.4f, 0f, 2.6f), // entrance (automatic doors stand open)
            new WallOpening(11.8f, 2.4f, 0.35f, 3.2f), new WallOpening(14.4f, 2.4f, 0.35f, 3.2f),
        };

        static void Shell()
        {
            var g = G("Store");
            var wallPaint = NumuInteriorMaterials.Paint("M_Market_Wall", NumuColor.Hex("#f1efe9"));
            var backWall = NumuInteriorMaterials.Paint("M_Market_WallBack", NumuColor.Hex("#dfe9e2"));
            var facade = NumuInteriorMaterials.Acg("M_Market_Facade", "Plaster001", 2f, NumuColor.Hex("#d9d4ca"), 0.3f);
            var floor = NumuInteriorMaterials.Acg("M_Market_Floor", "Tiles040", 1.2f, NumuColor.Hex("#f1efea"), 0.7f, 0.6f);
            var ceiling = NumuInteriorMaterials.Solid("M_Market_Ceiling", NumuColor.Hex("#f6f6f3"), 0.1f);
            var frameMat = NumuInteriorMaterials.Solid("M_Market_Aluminium", NumuColor.Hex("#9aa3ab"), 0.55f, 0.85f);
            var skirtingMat = NumuInteriorMaterials.Solid("M_Market_Skirting", NumuColor.Hex("#6f7a80"), 0.4f);

            var walls = new (Vector3 a, Vector3 b, WallOpening[] o, Material inner)[]
            {
                (new Vector3(X0, 0, Z0), new Vector3(X1, 0, Z0), Front, wallPaint),
                (new Vector3(X0, 0, Z0), new Vector3(X0, 0, Z1), new WallOpening[0], wallPaint),
                (new Vector3(X0, 0, Z1), new Vector3(X1, 0, Z1), new WallOpening[0], backWall),
                (new Vector3(X1, 0, Z0), new Vector3(X1, 0, Z1), new WallOpening[0], wallPaint),
            };
            var frame = new MeshBuilder();
            var glass = new MeshBuilder();
            var skirting = new MeshBuilder();
            int wi = 0;
            foreach (var w in walls)
            {
                var inner = new MeshBuilder();
                var outer = new MeshBuilder();
                NumuInteriorKit.Wall(inner, outer, w.a, w.b, Inside, H, T, w.o);
                S.MeshGO($"Wall_{wi}_Inside", g, inner, w.inner, true, ShadowCastingMode.TwoSided, true, 0.7f, true);
                S.MeshGO($"Wall_{wi}_Outside", g, outer, facade, true, ShadowCastingMode.On, true, 0.25f);
                NumuInteriorKit.Skirting(skirting, w.a, w.b, Inside, T, w.o, 0.12f);
                foreach (var o in w.o)
                    if (o.bottom > 0.01f) NumuInteriorKit.Window(frame, glass, null, w.a, w.b, Inside, o, T);
                wi++;
            }
            // Entrance: sliding glass leaves parked open beside the doorway, and a door header.
            var e = Front[3];
            float ex0 = X0 + e.start, ex1 = X0 + e.End;
            foreach (float x in new[] { ex0 - 0.62f, ex1 + 0.62f })
            {
                // Aluminium-framed glass leaf.
                NumuInteriorKit.Box(frame, new Vector3(x - 0.6f, 1.3f, IZ0 + 0.1f), new Vector3(0.05f, 2.6f, 0.05f));
                NumuInteriorKit.Box(frame, new Vector3(x + 0.6f, 1.3f, IZ0 + 0.1f), new Vector3(0.05f, 2.6f, 0.05f));
                NumuInteriorKit.Box(frame, new Vector3(x, 2.58f, IZ0 + 0.1f), new Vector3(1.24f, 0.06f, 0.05f));
                NumuInteriorKit.Box(frame, new Vector3(x, 0.04f, IZ0 + 0.1f), new Vector3(1.24f, 0.08f, 0.05f));
                NumuInteriorKit.Quad(glass, new Vector3(x - 0.58f, 0.06f, IZ0 + 0.13f), Vector3.right * 1.16f, Vector3.up * 2.48f, Vector3.forward);
                NumuInteriorKit.Quad(glass, new Vector3(x + 0.58f, 0.06f, IZ0 + 0.07f), Vector3.left * 1.16f, Vector3.up * 2.48f, Vector3.back);
            }
            S.MeshGO("WindowFrames", g, frame, frameMat, true, ShadowCastingMode.On, false);
            var glassGo = S.MeshGO("Glass", g, glass, NumuInteriorMaterials.Glass(), false, ShadowCastingMode.Off, false);
            GameObjectUtility.SetStaticEditorFlags(glassGo, StaticEditorFlags.BatchingStatic);
            // Glass stops the follow camera (it would otherwise slip outside through the window).
            glassGo.AddComponent<MeshCollider>();
            S.MeshGO("Skirting", g, skirting, skirtingMat, true, ShadowCastingMode.Off, false);

            var fl = new MeshBuilder();
            NumuInteriorKit.Slab(fl, Rect.MinMaxRect(X0, Z0, X1, Z1), 0f, true);
            S.MeshGO("Floor", g, fl, floor, true, ShadowCastingMode.Off, true, 0.7f, true);
            var ce = new MeshBuilder();
            NumuInteriorKit.Slab(ce, Rect.MinMaxRect(X0, Z0, X1, Z1), H, false);
            S.MeshGO("Ceiling", g, ce, ceiling, true, ShadowCastingMode.TwoSided, true, 0.8f);
            var roof = new MeshBuilder();
            NumuInteriorKit.Box(roof, new Vector3(0, H + 0.22f, 0), new Vector3(X1 - X0 + 0.8f, 0.4f, Z1 - Z0 + 0.8f), false);
            S.MeshGO("Roof", g, roof, facade, true, ShadowCastingMode.TwoSided, false);

            // Linear LED fittings in rows over the aisles.
            var leds = new MeshBuilder();
            var housings = new MeshBuilder();
            foreach (var p in LedSpots())
            {
                NumuInteriorKit.Box(housings, new Vector3(p.x, H - 0.45f, p.y), new Vector3(0.16f, 0.06f, 2.4f));
                NumuInteriorKit.Quad(leds, new Vector3(p.x - 0.06f, H - 0.481f, p.y - 1.15f), Vector3.right * 0.12f, Vector3.forward * 2.3f, Vector3.down);
                Shapes.Beam(housings, new Vector3(p.x, H - 0.42f, p.y - 1f), new Vector3(p.x, H, p.y - 1f), 0.004f, 4, Color.white);
                Shapes.Beam(housings, new Vector3(p.x, H - 0.42f, p.y + 1f), new Vector3(p.x, H, p.y + 1f), 0.004f, 4, Color.white);
            }
            S.MeshGO("LedHousings", g, housings, frameMat, true, ShadowCastingMode.Off, false);
            S.MeshGO("LedStrips", g, leds, NumuInteriorMaterials.Emissive("M_Market_Led", NumuColor.Hex("#f5f8ff"), 2.2f), true, ShadowCastingMode.Off, false);

            // Car park in front of the store.
            var outdoor = G("CarPark");
            var asphalt = NumuInteriorMaterials.Acg("M_Market_Asphalt", "Plaster001", 1.5f, NumuColor.Hex("#5a5c60"), 0.25f, 1.2f);
            var lot = new MeshBuilder();
            NumuInteriorKit.Slab(lot, Rect.MinMaxRect(-20f, -32f, 20f, Z0 - 3.2f), 0.0f, true);
            S.MeshGO("Asphalt", outdoor, lot, asphalt, true, ShadowCastingMode.Off, true, 0.15f);
            var pave = new MeshBuilder();
            NumuInteriorKit.Slab(pave, Rect.MinMaxRect(-20f, Z0 - 3.2f, 20f, Z0), 0.03f, true);
            S.MeshGO("Pavement", outdoor, pave, NumuWorldMaterials.Load<Material>($"{NumuWorldMaterials.Dir}/M_PavingStones138.mat"), true, ShadowCastingMode.Off, true, 0.3f);
            var lines = new MeshBuilder();
            for (float x = -16f; x <= 16f; x += 2.6f)
                NumuInteriorKit.Box(lines, new Vector3(x, 0.005f, Z0 - 7.5f), new Vector3(0.1f, 0.01f, 4.6f), false);
            S.MeshGO("ParkingLines", outdoor, lines, NumuInteriorMaterials.Solid("M_Market_LinePaint", NumuColor.Hex("#ecebe4"), 0.3f), true, ShadowCastingMode.Off, false);
            var lawn = NumuInteriorMaterials.Acg("M_Lawn", "Grass004", 2.5f, NumuColor.Hex("#c9d8b0"), 0.3f);
            S.Outdoors(Rect.MinMaxRect(X0, Z0, X1, Z1), lawn, 12, 33, 5f, 20f);
            S.Log.AppendLine("Shell: 24×18 m store, glass storefront with open sliding doors, large-format floor tiles, linear LED lighting, car park.");
        }

        static IEnumerable<Vector2> LedSpots()
        {
            foreach (float x in new[] { -8.5f, -4.5f, -0.5f, 3.5f, 7.5f })
                foreach (float z in new[] { -5.5f, -1.5f, 2.5f, 6.5f })
                    yield return new Vector2(x, z);
        }

        // ------------------------------------------------------------------ aisles

        static readonly string[][] AisleFamilies =
        {
            new[] { "boxes", "boxes" },        // breakfast & cereal
            new[] { "cans", "boxes" },         // pasta, rice & cans
            new[] { "boxes", "bottles" },      // snacks / sauces
            new[] { "bottles", "household" },  // drinks / household
        };

        static void Aisles()
        {
            var g = G("Aisles");
            var metal = NumuInteriorMaterials.Solid("M_Market_GondolaWhite", NumuColor.Hex("#eef0f0"), 0.5f, 0.2f);
            var back = NumuInteriorMaterials.Solid("M_Market_GondolaBack", NumuColor.Hex("#d8dee2"), 0.3f);
            var strip = PriceStripMaterial();
            var rng = new System.Random(77);
            int stock = 0;
            float len = GondolaZ1 - GondolaZ0;
            for (int a = 0; a < AisleX.Length; a++)
            {
                float x = AisleX[a];
                var unit = G($"Gondola_{a}", g);
                var body = new MeshBuilder();
                var panel = new MeshBuilder();
                var strips = new MeshBuilder();
                NumuInteriorKit.Box(body, new Vector3(x, 0.08f, (GondolaZ0 + GondolaZ1) * 0.5f), new Vector3(1.1f, 0.16f, len));
                NumuInteriorKit.Box(panel, new Vector3(x, 1.06f, (GondolaZ0 + GondolaZ1) * 0.5f), new Vector3(0.06f, 1.8f, len));
                // Uprights every 1.25 m.
                for (float z = GondolaZ0; z <= GondolaZ1 + 0.01f; z += len / 6f)
                    NumuInteriorKit.Box(body, new Vector3(x, 1.06f, z), new Vector3(0.08f, 1.8f, 0.04f));
                float[] levels = { 0.18f, 0.55f, 0.92f, 1.29f, 1.66f };
                foreach (float side in new[] { -1f, 1f })
                {
                    var stockMb = new MeshBuilder();
                    string family = AisleFamilies[a][side < 0 ? 0 : 1];
                    for (int l = 0; l < levels.Length; l++)
                    {
                        float y = levels[l];
                        float depth = l == 0 ? 0.5f : 0.42f;
                        float cx = x + side * (0.03f + depth * 0.5f);
                        NumuInteriorKit.Box(body, new Vector3(cx, y - 0.012f, (GondolaZ0 + GondolaZ1) * 0.5f), new Vector3(depth, 0.024f, len - 0.04f));
                        // Price strip on the shelf edge, mapped along its length.
                        var sq = new MeshBuilder();
                        NumuInteriorKit.Quad(sq, new Vector3(x + side * (0.03f + depth + 0.002f), y - 0.04f, side > 0 ? GondolaZ0 : GondolaZ1), (side > 0 ? Vector3.forward : Vector3.back) * len, Vector3.up * 0.045f, Vector3.right * side);
                        for (int v = 0; v < sq.UVs.Count; v++) sq.UVs[v] = new Vector2(sq.UVs[v].x / 1.25f, sq.UVs[v].y / 0.045f);
                        strips.Append(sq);
                        // Stock: shelf frame x along the gondola, z towards the aisle (yaw 90 faces +x and runs towards -z).
                        var frame = Matrix4x4.TRS(new Vector3(cx, y, side > 0 ? GondolaZ1 - 0.03f : GondolaZ0 + 0.03f), Quaternion.Euler(0, side > 0 ? 90 : -90, 0), Vector3.one);
                        float clearance = l == levels.Length - 1 ? 0.4f : levels[l + 1] - y - 0.03f;
                        stock += NumuProductKit.FillShelf(stockMb, frame, len - 0.06f, depth, clearance, family, rng);
                    }
                    var stockGo = S.MeshGO($"Stock_{(side < 0 ? "W" : "E")}", unit, stockMb, _products, true, ShadowCastingMode.Off, false);
                    if (stockGo != null)
                    {
                        var lod = stockGo.AddComponent<LODGroup>();
                        lod.SetLODs(new[] { new LOD(0.035f, new Renderer[] { stockGo.GetComponent<Renderer>() }) });
                    }
                }
                var bodyGo = S.MeshGO("Shelving", unit, body, metal, true, ShadowCastingMode.On, true, 0.6f);
                NumuWorldScaffold.Solid(bodyGo, new Vector3(x, 1.0f, (GondolaZ0 + GondolaZ1) * 0.5f), new Vector3(1.0f, 2.0f, len));
                S.MeshGO("BackPanel", unit, panel, back, true, ShadowCastingMode.On, true, 0.6f);
                S.MeshGO("PriceStrips", unit, strips, strip, true, ShadowCastingMode.Off, false);
            }
            S.Log.AppendLine($"Aisles: {AisleX.Length} double-sided gondolas, 5 shelves a side with price strips; {stock} stocked products from a generated packaging atlas.");
        }

        static Material PriceStripMaterial() => NumuInteriorMaterials.Textured("M_Market_PriceStrip", Tex("PriceStrip"), 0.4f);

        // ------------------------------------------------------------------ chillers

        static void Chillers()
        {
            var g = G("Chillers");
            var body = new MeshBuilder();
            var frames = new MeshBuilder();
            var glass = new MeshBuilder();
            var light = new MeshBuilder();
            var stock = new MeshBuilder();
            var rng = new System.Random(5);
            float x0 = -10f, x1 = 10f, depth = 0.8f, hgt = 2.15f, zb = IZ1;
            // Open-fronted cabinet: plinth, canopy and end panels; the front is glass doors.
            NumuInteriorKit.Box(body, new Vector3(0, 0.06f, zb - depth * 0.5f), new Vector3(x1 - x0, 0.12f, depth));
            NumuInteriorKit.Box(body, new Vector3(0, hgt + 0.08f, zb - depth * 0.5f), new Vector3(x1 - x0 + 0.1f, 0.2f, depth + 0.05f));
            NumuInteriorKit.Box(body, new Vector3(x0 - 0.03f, hgt * 0.5f, zb - depth * 0.5f), new Vector3(0.06f, hgt, depth));
            NumuInteriorKit.Box(body, new Vector3(x1 + 0.03f, hgt * 0.5f, zb - depth * 0.5f), new Vector3(0.06f, hgt, depth));
            int doors = 16;
            float dw = (x1 - x0) / doors;
            var inside = new MeshBuilder();
            NumuInteriorKit.Quad(inside, new Vector3(x0, 0.12f, zb - 0.03f), Vector3.right * (x1 - x0), Vector3.up * 1.9f, Vector3.back);
            for (int i = 0; i < doors; i++)
            {
                float cx = x0 + dw * (i + 0.5f);
                NumuInteriorKit.Box(frames, new Vector3(cx - dw * 0.5f + 0.02f, 1.08f, zb - depth - 0.02f), new Vector3(0.04f, 1.96f, 0.05f));
                NumuInteriorKit.Box(frames, new Vector3(cx + dw * 0.32f, 1.1f, zb - depth - 0.06f), new Vector3(0.025f, 0.9f, 0.03f));
                NumuInteriorKit.Quad(glass, new Vector3(cx + dw * 0.5f - 0.02f, 0.12f, zb - depth - 0.03f), Vector3.left * (dw - 0.04f), Vector3.up * 1.92f, Vector3.back);
            }
            NumuInteriorKit.Box(frames, new Vector3(0, 2.08f, zb - depth - 0.02f), new Vector3(x1 - x0, 0.06f, 0.05f));
            NumuInteriorKit.Box(frames, new Vector3(0, 0.1f, zb - depth - 0.02f), new Vector3(x1 - x0, 0.06f, 0.05f));
            NumuInteriorKit.Quad(light, new Vector3(x0, 2.02f, zb - depth + 0.08f), Vector3.right * (x1 - x0), Vector3.forward * 0.05f, Vector3.down);
            // Shelves of milk, juice and yoghurt behind the doors (cut out of the body's front).
            foreach (float y in new[] { 0.2f, 0.62f, 1.04f, 1.46f })
            {
                NumuInteriorKit.Box(frames, new Vector3(0, y - 0.01f, zb - depth * 0.55f), new Vector3(x1 - x0, 0.02f, depth * 0.85f));
                var frame = Matrix4x4.TRS(new Vector3(x1 - 0.05f, y, zb - depth * 0.55f), Quaternion.Euler(0, 180, 0), Vector3.one);
                // One row is enough behind the glass doors.
                NumuProductKit.FillShelf(stock, frame, x1 - x0 - 0.1f, depth * 0.8f, 0.38f, y < 1f ? "cartons" : "bottles", rng, 0.97f, 1);
            }
            var bodyGo = S.MeshGO("ChillerBody", g, body, NumuInteriorMaterials.Solid("M_Market_ChillerBody", NumuColor.Hex("#2f3b45"), 0.4f), true, ShadowCastingMode.On, true, 0.5f);
            NumuWorldScaffold.Solid(bodyGo, new Vector3(0, 1.1f, zb - depth * 0.5f - 0.05f), new Vector3(x1 - x0, 2.2f, depth + 0.1f));
            S.MeshGO("ChillerInside", g, inside, NumuInteriorMaterials.Solid("M_Market_ChillerInside", NumuColor.Hex("#e8eef2"), 0.6f), true, ShadowCastingMode.Off, false);
            S.MeshGO("ChillerFrames", g, frames, NumuInteriorMaterials.Solid("M_Market_ChillerFrames", NumuColor.Hex("#b4bcc3"), 0.6f, 0.8f), true, ShadowCastingMode.Off, false);
            S.MeshGO("ChillerLight", g, light, NumuInteriorMaterials.Emissive("M_Market_ChillerLed", NumuColor.Hex("#eef6ff"), 2.5f), true, ShadowCastingMode.Off, false);
            S.MeshGO("ChillerStock", g, stock, _products, true, ShadowCastingMode.Off, false);
            var gl = S.MeshGO("ChillerGlass", g, glass, NumuInteriorMaterials.Glass("M_Market_ChillerGlass"), false, ShadowCastingMode.Off, false);
            GameObjectUtility.SetStaticEditorFlags(gl, StaticEditorFlags.BatchingStatic);
            // Glass stops the follow camera (it would otherwise slip outside through the window).
            gl.AddComponent<MeshCollider>();

            // Bakery shelves on the west wall.
            var bakery = G("Bakery", g);
            foreach (float z in new[] { -0.5f, 1.8f })
            {
                var shelf = S.Place("steel_frame_shelves_01", bakery, new Vector3(IX0 + 0.3f, 0, z), 90f, 1f, true);
                if (shelf == null) continue;
                var b = NumuWorldScaffold.Bounds(shelf);
                shelf.transform.position += new Vector3(IX0 + 0.02f - b.min.x, 0, 0);
                b = NumuWorldScaffold.Bounds(shelf);
                // A few scanned rolls at eye level; procedural loaves fill the other shelves cheaply.
                for (int k = 0; k < 3; k++)
                    S.Place("hamburger_buns_one", bakery, new Vector3(b.center.x, 0.94f, b.min.z + 0.25f + k * 0.3f), rng.Next(360), 1.1f);
                var loaves = new MeshBuilder();
                foreach (float y in new[] { 0.46f, 1.42f })
                    for (int k = 0; k < 5; k++)
                    {
                        var part = new MeshBuilder();
                        var lp = new Vector3(b.center.x + (float)(rng.NextDouble() - 0.5) * 0.12f, y + 0.045f, b.min.z + 0.15f + k * 0.19f);
                        Shapes.Ellipsoid(part, Matrix4x4.TRS(lp, Quaternion.Euler(0, 90 + rng.Next(-10, 10), 0), Vector3.one), new Vector3(0.13f, 0.055f, 0.075f), Color.white, 2);
                        var uv = new Vector2((rng.Next(8) + 0.5f) / 8f, 5.5f / 8f);
                        for (int v = 0; v < part.UVs.Count; v++) part.UVs[v] = uv;
                        loaves.Append(part);
                    }
                S.MeshGO("Loaves", bakery, loaves, _produce, true, ShadowCastingMode.On, false);
            }
            S.Log.AppendLine("Chillers: 20 m of glass-door dairy and drink fridges, lit and stocked. Bakery shelves with bread rolls.");
        }

        // ------------------------------------------------------------------ produce

        static void Produce()
        {
            var g = G("Produce");
            var wood = NumuInteriorMaterials.Acg("M_Market_DisplayWood", "Wood049", 1.2f, NumuColor.Hex("#d6b48c"), 0.4f);
            var tables = new MeshBuilder();
            var piles = new MeshBuilder();
            var rng = new System.Random(19);
            int[] types = { 0, 2, 3, 1, 4, 5, 6, 7, 2, 0, 5, 3 };
            int t = 0;
            foreach (var c in new[] { new Vector3(-9.6f, 0, -5.4f), new Vector3(-6.4f, 0, -5.4f), new Vector3(-9.6f, 0, -2.8f), new Vector3(-6.4f, 0, -2.8f) })
            {
                // Table: legs, apron, and an angled top holding three crates.
                foreach (var l in new[] { new Vector3(-1.1f, 0, -0.45f), new Vector3(1.1f, 0, -0.45f), new Vector3(-1.1f, 0, 0.45f), new Vector3(1.1f, 0, 0.45f) })
                    NumuInteriorKit.Box(tables, c + l + new Vector3(0, 0.38f, 0), new Vector3(0.06f, 0.76f, 0.06f));
                NumuInteriorKit.Box(tables, c + new Vector3(0, 0.78f, 0), new Vector3(2.4f, 0.06f, 1.0f));
                for (int k = 0; k < 3; k++)
                {
                    var cp = c + new Vector3(-0.8f + k * 0.8f, 0.81f, 0);
                    var crate = S.Place("wooden_crate_01_open", g, cp, 90f, 0.92f);
                    // Heaped above the rim, the way stores display fresh produce.
                    float floorY = cp.y + 0.17f;
                    NumuProductKit.Pile(piles, new Vector3(cp.x, floorY, cp.z), new Vector2(0.32f, 0.68f), 0.18f, types[t % types.Length], 0.038f, rng);
                    t++;
                }
            }
            var tablesGo = S.MeshGO("DisplayTables", g, tables, wood, true, ShadowCastingMode.On, true, 1f);
            foreach (var c in new[] { new Vector3(-8.0f, 0, -5.4f), new Vector3(-8.0f, 0, -2.8f) }) NumuWorldScaffold.Solid(tablesGo, c + Vector3.up * 0.5f, new Vector3(5.6f, 1.0f, 1.1f));
            S.MeshGO("ProducePiles", g, piles, _produce, true, ShadowCastingMode.On, false);
            // Hero produce (scanned) on top of the piles at the front.
            S.Place("bananas_bunch", g, new Vector3(-10.4f, 1.12f, -5.5f), 20f);
            S.Place("bananas_bunch", g, new Vector3(-10.25f, 1.12f, -5.25f), 160f);
            S.Place("lemon", g, new Vector3(-7.2f, 1.1f, -5.5f), 40f);
            S.Place("sweet_potato", g, new Vector3(-5.6f, 1.1f, -2.9f), 80f, 1.1f);
            S.Place("food_pears_asian_01_a", g, new Vector3(-6.4f, 1.1f, -2.75f), 10f);
            S.Place("food_apple_01", g, new Vector3(-8.85f, 1.1f, -2.8f), 0f);
            S.Place("food_avocado_01", g, new Vector3(-9.6f, 1.1f, -2.9f), 0f);
            S.Place("food_kiwi_01", g, new Vector3(-6.3f, 1.1f, -5.3f), 0f);
            S.Place("yellow_onion", g, new Vector3(-8.8f, 1.1f, -5.35f), 0f);
            S.Log.AppendLine("Produce: four display tables with 12 crates of fruit and vegetables (procedural piles + scanned hero produce).");
        }

        // ------------------------------------------------------------------ checkouts

        static GameObject _trolleyAnchor;

        static void Checkouts()
        {
            var g = G("Checkouts");
            var counterMat = NumuInteriorMaterials.Acg("M_Market_Counter", "Wood051", 1.2f, NumuColor.Hex("#c9b49a"), 0.45f);
            var topMat = NumuInteriorMaterials.Solid("M_Market_CounterTop", NumuColor.Hex("#dcdcd8"), 0.6f, 0.6f);
            var belt = NumuInteriorMaterials.Acg("M_Market_Belt", "Rubber004", 0.5f, NumuColor.Hex("#3a3a3c"), 0.25f);
            var dark = NumuInteriorMaterials.Solid("M_Market_Device", NumuColor.Hex("#26282c"), 0.6f);
            var screen = NumuInteriorMaterials.Emissive("M_Market_PosScreen", NumuColor.Hex("#7fb6e8"), 0.6f);
            foreach (float cx in new[] { CounterX, CounterX + 3.5f })
            {
                var body = new MeshBuilder();
                var top = new MeshBuilder();
                var beltMb = new MeshBuilder();
                var dev = new MeshBuilder();
                var scr = new MeshBuilder();
                float z0 = -6.9f, z1 = -3.9f;
                NumuInteriorKit.Box(body, new Vector3(cx, 0.45f, (z0 + z1) * 0.5f), new Vector3(0.7f, 0.9f, z1 - z0));
                NumuInteriorKit.Box(top, new Vector3(cx, 0.915f, (z0 + z1) * 0.5f), new Vector3(0.74f, 0.03f, z1 - z0 + 0.04f));
                NumuInteriorKit.Box(beltMb, new Vector3(cx, 0.94f, -4.75f), new Vector3(0.5f, 0.02f, 1.6f));
                NumuInteriorKit.Box(dev, new Vector3(cx, 0.96f, -5.75f), new Vector3(0.4f, 0.06f, 0.3f));
                // Cashier screen on a post, card terminal, and the bagging shelf.
                NumuInteriorKit.Box(dev, new Vector3(cx + 0.42f, 1.05f, -5.9f), new Vector3(0.04f, 0.3f, 0.04f));
                NumuInteriorKit.Box(dev, new Vector3(cx + 0.42f, 1.28f, -5.9f), new Vector3(0.04f, 0.26f, 0.36f));
                NumuInteriorKit.Quad(scr, new Vector3(cx + 0.399f, 1.17f, -6.06f), Vector3.forward * 0.32f, Vector3.up * 0.22f, Vector3.left);
                NumuInteriorKit.Box(dev, new Vector3(cx - 0.3f, 1.0f, -6.15f), new Vector3(0.09f, 0.08f, 0.16f));
                NumuInteriorKit.Box(top, new Vector3(cx - 0.05f, 0.75f, -6.55f), new Vector3(0.6f, 0.03f, 0.6f));
                // Lane light pole with its number.
                NumuInteriorKit.Box(dev, new Vector3(cx - 0.3f, 1.4f, -3.95f), new Vector3(0.05f, 1.0f, 0.05f));
                var bodyGo = S.MeshGO($"Counter_{cx}", g, body, counterMat, true, ShadowCastingMode.On, true, 1f);
                NumuWorldScaffold.Solid(bodyGo, new Vector3(cx, 0.5f, (z0 + z1) * 0.5f), new Vector3(0.74f, 1.0f, z1 - z0));
                S.MeshGO($"CounterTop_{cx}", g, top, topMat, true, ShadowCastingMode.On, false);
                S.MeshGO($"Belt_{cx}", g, beltMb, belt, true, ShadowCastingMode.Off, false);
                S.MeshGO($"Devices_{cx}", g, dev, dark, true, ShadowCastingMode.On, false);
                S.MeshGO($"Screen_{cx}", g, scr, screen, true, ShadowCastingMode.Off, false);
                bool open = Mathf.Approximately(cx, CounterX);
                TexturedQuad($"LaneSign_{(open ? 1 : 2)}", g, NumuInteriorMaterials.Textured($"M_Market_Lane{(open ? 1 : 2)}", Tex($"Lane{(open ? 1 : 2)}"), 0.3f),
                    new Vector3(cx - 0.3f, 2.05f, -3.95f), Vector3.forward, new Vector2(0.32f, 0.32f), true);
            }
            // Items on the belt: the shopper ahead is being served.
            var items = new MeshBuilder();
            var rng = new System.Random(8);
            for (int i = 0; i < 5; i++)
                NumuProductKit.Add(items, NumuProductKit.Random(rng, i % 2 == 0 ? "boxes" : "cans"), new Vector3(CounterX + (float)(rng.NextDouble() - 0.5) * 0.2f, 0.95f, -4.2f - i * 0.28f), Quaternion.Euler(0, 90 + rng.Next(-15, 15), 0));
            S.MeshGO("BeltItems", g, items, _products, true, ShadowCastingMode.On, false);
            S.Place("long_life_food_milk", g, new Vector3(CounterX - 0.05f, 0.95f, -5.2f), 80f);
            S.Place("long_life_food_tomatoes", g, new Vector3(CounterX + 0.1f, 0.95f, -5.4f), 20f);

            // The queueing shopper's trolley, and a line of trolleys and baskets by the entrance.
            _trolleyAnchor = Trolley(g, new Vector3(LaneX, 0, -5.0f), 180f, true, rng);
            for (int i = 0; i < 3; i++) Trolley(g, new Vector3(-6.0f - i * 0.18f, 0, -7.9f), 90f, false, rng);
            BasketStack(g, new Vector3(-3.9f, 0, -7.7f));
            S.Log.AppendLine("Checkouts: two lanes (1 open, 2 closed) with belts, scanners, screens and card readers; trolleys and baskets by the entrance.");
        }

        /// <summary>Wire shopping trolley (procedural), optionally loaded with groceries. Origin on the floor, handle at -z.</summary>
        static GameObject Trolley(Transform parent, Vector3 pos, float yaw, bool loaded, System.Random rng)
        {
            var wire = new MeshBuilder();
            var plastic = new MeshBuilder();
            float w = 0.52f, l = 0.82f, y0 = 0.42f, y1 = 0.95f;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            // Basket: perimeter rails, vertical wires, floor grid.
            foreach (float y in new[] { y0, y0 + 0.18f, y0 + 0.36f, y1 })
            {
                float taper = (y - y0) * 0.12f;
                Shapes.Beam(wire, P(-w / 2 - taper, y, -l / 2), P(w / 2 + taper, y, -l / 2), 0.005f, 4, Color.white);
                Shapes.Beam(wire, P(-w / 2 - taper, y, l / 2 + taper), P(w / 2 + taper, y, l / 2 + taper), 0.005f, 4, Color.white);
                Shapes.Beam(wire, P(-w / 2 - taper, y, -l / 2), P(-w / 2 - taper, y, l / 2 + taper), 0.005f, 4, Color.white);
                Shapes.Beam(wire, P(w / 2 + taper, y, -l / 2), P(w / 2 + taper, y, l / 2 + taper), 0.005f, 4, Color.white);
            }
            float tt = (y1 - y0) * 0.12f;
            for (float x = -w / 2; x <= w / 2 + 0.01f; x += 0.065f)
            {
                Shapes.Beam(wire, P(x, y0, -l / 2), P(x * (1 + tt), y1, -l / 2), 0.004f, 4, Color.white);
                Shapes.Beam(wire, P(x, y0, l / 2), P(x * (1 + tt), y1, l / 2 + tt), 0.004f, 4, Color.white);
                Shapes.Beam(wire, P(x, y0, -l / 2), P(x, y0, l / 2), 0.004f, 4, Color.white);
            }
            for (float z = -l / 2; z <= l / 2 + 0.01f; z += 0.065f)
                foreach (float s in new[] { -1f, 1f })
                    Shapes.Beam(wire, P(s * w / 2, y0, z), P(s * (w / 2 + tt), y1, z + tt * (z + l / 2) / l), 0.004f, 4, Color.white);
            // Chassis, legs, wheels and the handle.
            foreach (float s in new[] { -1f, 1f })
            {
                Shapes.Beam(wire, P(s * 0.22f, 0.12f, -l / 2), P(s * 0.2f, 0.12f, l / 2 + 0.05f), 0.012f, 6, Color.white);
                Shapes.Beam(wire, P(s * 0.22f, 0.12f, -l / 2), P(s * (w / 2 + tt), y1 + 0.08f, -l / 2 - 0.12f), 0.012f, 6, Color.white);
                foreach (float z in new[] { -l / 2 + 0.05f, l / 2 })
                    Shapes.Cylinder(plastic, Matrix4x4.TRS(P(s * 0.21f, 0.06f, z), Quaternion.Euler(0, 0, 90), Vector3.one), 0.055f, 0.055f, 0.035f, 10, Color.white);
            }
            Shapes.Cylinder(plastic, Matrix4x4.TRS(P(0, y1 + 0.08f, -l / 2 - 0.12f), Quaternion.Euler(0, 0, 90), Vector3.one), 0.018f, 0.018f, w + 0.12f, 10, Color.white);
            var go = new GameObject("Trolley");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var m = go.transform.localToWorldMatrix;
            var wireW = new MeshBuilder();
            wireW.Append(wire, m);
            var plasticW = new MeshBuilder();
            plasticW.Append(plastic, m);
            S.MeshGO("Wire", go.transform.parent, wireW, NumuInteriorMaterials.Solid("M_Market_Chrome", NumuColor.Hex("#c9ced3"), 0.75f, 0.95f), true, ShadowCastingMode.On, false).transform.SetParent(go.transform, true);
            S.MeshGO("Plastic", go.transform.parent, plasticW, NumuInteriorMaterials.Solid("M_Market_TrolleyPlastic", NumuColor.Hex("#d23c32"), 0.45f), true, ShadowCastingMode.On, false).transform.SetParent(go.transform, true);
            if (loaded)
            {
                var goods = new MeshBuilder();
                for (int i = 0; i < 9; i++)
                {
                    var p = NumuProductKit.Random(rng, i % 3 == 0 ? "bottles" : i % 3 == 1 ? "boxes" : "cans");
                    var lp = P((float)(rng.NextDouble() - 0.5) * 0.36f, y0 + 0.01f, (float)(rng.NextDouble() - 0.5) * 0.6f);
                    NumuProductKit.Add(goods, p, m.MultiplyPoint3x4(lp), go.transform.rotation * Quaternion.Euler(rng.Next(-8, 8), rng.Next(360), rng.Next(-8, 8)));
                }
                S.MeshGO("Groceries", go.transform.parent, goods, _products, true, ShadowCastingMode.On, false).transform.SetParent(go.transform, true);
                NumuWorldScaffold.Solid(go, new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 1.0f));
            }
            return go;
        }

        static void BasketStack(Transform parent, Vector3 pos)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 6; i++)
            {
                float y = 0.02f + i * 0.06f;
                var outline = new[] { new Vector3(-0.22f, 0, -0.16f), new Vector3(0.22f, 0, -0.16f), new Vector3(0.22f, 0, 0.16f), new Vector3(-0.22f, 0, 0.16f) };
                for (int k = 0; k < 4; k++)
                {
                    Vector3 a = outline[k], b = outline[(k + 1) % 4];
                    NumuInteriorKit.Box(mb, Matrix4x4.TRS(pos + Vector3.up * y + (a + b) * 0.5f * 1.08f + Vector3.up * 0.1f, Quaternion.LookRotation(b - a) * Quaternion.Euler(0, 90, 0), Vector3.one), Vector3.zero, new Vector3((b - a).magnitude * 1.08f, 0.22f, 0.012f));
                }
            }
            S.MeshGO("Baskets", parent, mb, NumuInteriorMaterials.Solid("M_Market_BasketRed", NumuColor.Hex("#c8372d"), 0.5f), true, ShadowCastingMode.On, false);
        }

        // ------------------------------------------------------------------ signs

        static void Signs()
        {
            var g = G("Signs");
            var cable = new MeshBuilder();
            for (int i = 0; i < AisleX.Length; i++)
            {
                var c = new Vector3(AisleX[i], 2.95f, GondolaZ0 - 0.2f);
                TexturedQuad($"AisleSign_{i}", g, SignMaterial($"Aisle_{i}"), c, Vector3.back, new Vector2(1.6f, 0.4f), true);
                Shapes.Beam(cable, c + new Vector3(-0.6f, 0.2f, 0), new Vector3(c.x - 0.6f, H, c.z), 0.004f, 4, Color.white);
                Shapes.Beam(cable, c + new Vector3(0.6f, 0.2f, 0), new Vector3(c.x + 0.6f, H, c.z), 0.004f, 4, Color.white);
            }
            void Hanging(string name, Vector3 c, Vector3 facing, float width)
            {
                TexturedQuad(name, g, SignMaterial(name), c, facing, new Vector2(width, width / 4f), true);
                Vector3 side = Vector3.Cross(Vector3.up, facing).normalized * (width * 0.38f);
                Shapes.Beam(cable, c - side + Vector3.up * width / 8f, new Vector3(c.x, H, c.z) - side, 0.004f, 4, Color.white);
                Shapes.Beam(cable, c + side + Vector3.up * width / 8f, new Vector3(c.x, H, c.z) + side, 0.004f, 4, Color.white);
            }
            Hanging("Sign_Produce", new Vector3(-8f, 3.1f, -4.1f), Vector3.back, 2.4f);
            Hanging("Sign_Dairy", new Vector3(0f, 3.1f, IZ1 - 1.6f), Vector3.back, 2.6f);
            Hanging("Sign_Bakery", new Vector3(IX0 + 1.4f, 3.1f, 0.65f), Vector3.right, 1.6f);
            Hanging("Sign_Checkout", new Vector3(CounterX + 1.7f, 3.0f, -3.4f), Vector3.back, 2.0f);
            S.MeshGO("SignCables", g, cable, NumuInteriorMaterials.Solid("M_Market_Cable", NumuColor.Hex("#8c9298"), 0.5f, 0.8f), true, ShadowCastingMode.Off, false);

            TexturedQuad("WelcomeSign", g, NumuInteriorMaterials.Textured("M_Market_Welcome", Tex("Welcome"), 0.3f), new Vector3(-1.8f, 3.45f, IZ0 + 0.04f), Vector3.forward, new Vector2(3.2f, 0.8f), false);
        }

        // ------------------------------------------------------------------ lighting

        static void Lighting()
        {
            // Mid-morning sun through the storefront.
            S.Sun(new Vector3(42f, 15f, 0f), NumuColor.Hex("#fff3e2"), 1.9f);
            S.Sky("Assets/ThirdParty/PolyHaven/HDRI/farm_field_puresky_2k.hdr", 1.0f, 30f, new Color(0.5f, 0.5f, 0.5f));
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;
            RenderSettings.fog = false;
            Color cool = NumuColor.Hex("#f4f7ff");
            int i = 0;
            foreach (var p in LedSpots()) S.AreaLight($"Led_{i++}", new Vector3(p.x, H - 0.5f, p.y), new Vector3(90, 0, 0), new Vector2(0.4f, 2.3f), cool, 6.5f, 14f);
            S.AreaLight("ChillerGlow", new Vector3(0, 1.5f, IZ1 - 0.85f), new Vector3(0, 180, 0), new Vector2(20f, 1.8f), NumuColor.Hex("#e8f2ff"), 0.8f);
            S.AreaLight("Sky_Storefront", new Vector3(-4f, 1.6f, IZ0 + 0.2f), Vector3.zero, new Vector2(15f, 2.4f), NumuColor.Hex("#d6e4ff"), 0.8f);
            S.Uplight(Rect.MinMaxRect(IX0, IZ0, IX1, IZ1), NumuColor.Hex("#f3f3ee"), 0.7f);
            S.ReflectionProbe("ReflectionProbe", new Vector3(0, H * 0.5f, 0), new Vector3(X1 - X0, H, Z1 - Z0));
            S.LightProbes(Rect.MinMaxRect(IX0 + 0.4f, IZ0 + 0.4f, IX1 - 0.4f, IZ1 - 0.4f), 2f, new[] { 0.4f, 1.3f, 2.6f },
                p => !(Mathf.Abs(p.z - (GondolaZ0 + GondolaZ1) * 0.5f) < (GondolaZ1 - GondolaZ0) * 0.5f && System.Array.Exists(AisleX, x => Mathf.Abs(p.x - x) < 0.6f)));
            S.PostProcessing(0.0f, 9f, 6f, 1f, 0.2f, 0.15f);
        }

        // ------------------------------------------------------------------ people and the activity

        static void People()
        {
            var g = G("Characters");
            var (player, follow) = S.CreatePlayer(g, PlayerStart, 0f, 3.4f, 1.9f);
            var interactions = S.CreateInteractions(player.transform);

            var cashier = S.SpawnCharacter("Female_Adult_12", "Nora", g, new Vector3(CounterX + 0.85f, 0, -5.55f), 270f);
            var cashierNpc = NumuWorldScaffold.MakeNpc(cashier, "Idle", player.transform, "Nod", "LookAround");
            cashier.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Happy, 0.35f);
            NumuWorldScaffold.Blocker(cashier, 0.28f, 1.75f);

            var paying = S.SpawnCharacter("Female_Adult_01", "Shopper paying", g, new Vector3(LaneX, 0, -6.0f), 90f);
            NumuWorldScaffold.MakeNpc(paying, "Documents", cashier.transform);
            NumuWorldScaffold.Blocker(paying, 0.28f, 1.7f);

            // The impatient shopper waits behind, hands on his trolley.
            var man = S.SpawnCharacter("Male_Adult_13", "Mr. Karim", g, new Vector3(LaneX, 0, -4.15f), 180f);
            man.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var manNpc = NumuWorldScaffold.MakeNpc(man, "Trolley", player.transform);
            man.GetComponent<NumuFacialExpressions>().SetExpression(NumuEmotion.Angry, 0.6f);
            NumuWorldScaffold.Blocker(man, 0.28f, 1.8f);

            var worker = S.SpawnCharacter("Male_Adult_02", "Shop assistant", g, new Vector3(AisleX[1] + 1.2f, 0, 2.4f), 90f);
            NumuWorldScaffold.MakeNpc(worker, "Idle", player.transform, "LookAround");
            NumuWorldScaffold.Blocker(worker, 0.28f, 1.8f);

            var manHead = NumuWorldScaffold.Head(man);
            var interaction = S.Interaction("Interaction_JoinTheLine", new Vector3(LaneX, 0, -2.75f), "Join the line", manHead, 1.5f);

            var go = new GameObject("Scenario_MarketCheckout");
            go.transform.SetParent(S.Root, false);
            var sc = go.AddComponent<NumuConversationScenario>();
            sc.scenarioId = "market_checkout";
            sc.world = "supermarket";
            sc.progressIsland = "supermarket";
            sc.progressLevel = 5;
            sc.introToast = "Time to pay! Find checkout 1 and join the line.";
            sc.doneToast = "Well done! You waited your turn and greeted the cashier. Tap Map to go back.";
            sc.actors.Add(new NumuConversationScenario.Actor { name = "Mr. Karim", npc = manNpc, face = man.GetComponent<NumuFacialExpressions>(), faceAnchor = manHead, turnToPlayer = true, shotDistance = 1.35f });
            sc.actors.Add(new NumuConversationScenario.Actor { name = "Nora", npc = cashierNpc, face = cashier.GetComponent<NumuFacialExpressions>(), faceAnchor = NumuWorldScaffold.Head(cashier), turnToPlayer = true, shotDistance = 1.3f });
            sc.interactable = interaction;
            sc.player = player;
            sc.followCamera = follow;
            sc.interactions = interactions;

            var L = NumuConversationScenario.StepKind.Line;
            var E = NumuConversationScenario.StepKind.Expression;
            var A = NumuConversationScenario.StepKind.Animation;
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 0, emotion = NumuEmotion.Angry, strength = 1f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "TalkAngry" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Oh, come on! This line is taking forever!" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "I've been waiting for ages!" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Angry" });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.EmotionQuestion, actor = 0, text = "How is Mr. Karim feeling?", emotion = NumuEmotion.Angry,
                praise = "Mr. Karim is angry, or frustrated. His eyebrows are pulled down and his mouth is tight.",
                hint = "Look at his eyebrows: they are pulled down low, and his voice is loud. Is that a calm, happy face?",
            });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.ChoiceQuestion, actor = 0, text = "You are waiting in line too. What should you do?",
                options = new List<NumuConversationScenario.Option>
                {
                    NumuHomeBuilder.Opt("Wait calmly for my turn", "Choice_Wait", "#dcecff"),
                    NumuHomeBuilder.Opt("Push to the front", "Choice_Push", "#ffdcd6"),
                    NumuHomeBuilder.Opt("Shout too", "Choice_Shout", "#fff1b8"),
                    NumuHomeBuilder.Opt("Run around the shop", "Choice_WalkAway", "#e8e6f0"),
                },
                correctOption = 0,
                praiseTitle = "Great waiting!",
                praise = "Standing calmly and taking turns is the right thing to do, even when others are upset.",
                hint = "Pushing, shouting or running around would make things harder for everyone. What is the calm way to wait?",
            });
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 0, emotion = NumuEmotion.Neutral, strength = 1f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = A, actor = 0, state = "Trolley" });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 0, text = "Hmm. You're right, waiting calmly is better. Sorry for shouting." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = E, actor = 1, emotion = NumuEmotion.Happy, strength = 0.85f });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 1, text = "Thank you both for waiting so nicely. Hello there!" });
            sc.steps.Add(new NumuConversationScenario.Step
            {
                kind = NumuConversationScenario.StepKind.ChoiceQuestion, actor = 1, text = "Nora the cashier says hello. What do you say?",
                options = new List<NumuConversationScenario.Option>
                {
                    NumuHomeBuilder.Opt("Hello! Thank you.", "Choice_Wave", "#dff3e3"),
                    NumuHomeBuilder.Opt("Say nothing", "Choice_Ignore", "#e8e6f0"),
                    NumuHomeBuilder.Opt("Give me my things!", "Choice_Shout", "#ffdcd6"),
                    NumuHomeBuilder.Opt("Walk away", "Choice_WalkAway", "#fff1b8"),
                },
                correctOption = 0,
                praiseTitle = "Lovely manners!",
                praise = "Saying hello and thank you makes people feel happy, and it's how we talk to a cashier.",
                hint = "When someone says hello, we say hello back. Which answer is friendly and polite?",
            });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = -1, text = "Hello! Thank you." });
            sc.steps.Add(new NumuConversationScenario.Step { kind = L, actor = 1, text = "You're very welcome. Have a lovely day!" });

            sc.ui = NumuWorldUIBuilder.Build(S.Root, new NumuWorldUIBuilder.Spec
            {
                canvasName = "Supermarket UI",
                title = "NUMU Market",
                subtitle = "Wait in line · Talk to cashier",
                chipIcon = NumuTextureKit.Sprite("Icon_Supermarket"),
                chipBg = NumuColor.Hex("#fff5c9"),
                chipInk = NumuColor.Hex("#8a3a12"),
                accent = NumuColor.Hex("#f39b2c"),
                prompt = "Join the line",
                speaker = "Nora",
                question = "How is Mr. Karim feeling?",
            }, interactions, player);
            S.Log.AppendLine("People: the child, Nora (cashier), a shopper paying, Mr. Karim (impatient, with trolley), a shop assistant. Activity market_checkout: 3 questions.");
        }
    }
}
