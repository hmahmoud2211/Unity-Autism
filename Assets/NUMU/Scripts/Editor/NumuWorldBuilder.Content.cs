using System.Collections.Generic;
using UnityEngine;

namespace Numu.EditorTools
{
    public static partial class NumuWorldBuilder
    {
        /// <summary>When false, landmarks are simple massing volumes for composition checks.</summary>
        public static bool DetailPass = true;

        static void BuildContent()
        {
            var content = Group("Content", _world);
            _keepClear.Clear();
            BuildConnections(content);
            BuildWaterfalls(content);
            if (DetailPass) BuildDetailedContent(content);
            else BuildBlockoutLandmarks(content);
        }

        static partial void BuildDetailedContentImpl(Transform parent);

        static void BuildDetailedContent(Transform parent) => BuildDetailedContentImpl(parent);

        /// <summary>Deck point just inside an island rim, at grass height.</summary>
        static Vector3 Deck(string id, float px, float py, float inset = 0.5f)
        {
            Vector3 r = Rim(id, px, py, out var o);
            Vector3 p = r - o * inset + Vector3.down * 0.04f;
            KeepClear(p, 2.0f);
            return p;
        }

        static void BuildConnections(Transform parent)
        {
            var g = Group("Bridges & Stairs", parent);
            var mb = new MeshBuilder();

            // Bridges.
            StructureKit.PlankBridge(mb, Deck("Home", 562, 348), Deck("Hub", 662, 446), 2.0f, 0.35f, 1);
            StructureKit.PlankBridge(mb, Deck("Home", 580, 280), Deck("School", 612, 236), 1.7f, 0.9f, 2, true);
            StructureKit.PlankBridge(mb, Deck("Landing", 962, 346, 0.6f), Deck("Hub", 892, 428), 2.0f, 0.3f, 3);
            StructureKit.PlankBridge(mb, Deck("Hub", 920, 492), Deck("Supermarket", 1043, 488), 2.0f, 0.25f, 4);
            Vector3 pl = Rim("Park", 1188, 322, out var po1) + po1 * 0.9f + Vector3.up * 0.1f;
            Vector3 pr = Rim("Park", 1240, 324, out var po2) + po2 * 0.9f + Vector3.up * 0.1f;
            StructureKit.PlankBridge(mb, pl, pr, 1.4f, 0.05f, 5);
            KeepClear(pl, 1.5f);
            KeepClear(pr, 1.5f);

            // Stairs.
            StructureKit.Stairs(mb, Deck("School", 882, 262, 0.8f), Deck("Landing", 958, 328, -0.2f) + Vector3.up * 0.05f, 1.8f, 11);
            StructureKit.Stairs(mb, Deck("Park", 1012, 300, 0.8f), Deck("Landing", 990, 326, -0.2f) + Vector3.up * 0.05f, 1.6f, 12);
            StructureKit.Stairs(mb, Deck("Home", 410, 392, 1.0f), G(418, 428, 2.2f) + Vector3.up * 0.05f, 1.6f, 13);

            // Hub dock and stairs down to it.
            Vector3 dockC = P(790, 650, 0.8f);
            float dockW = U(790, 650, 0.8f) * 150f;
            StructureKit.Deck(mb, dockC, Quaternion.identity, new Vector2(dockW, 5.5f), 0f, 21);
            Vector3 hubFront = Deck("Hub", 776, 560, 0.7f);
            StructureKit.Stairs(mb, hubFront, new Vector3(hubFront.x, 0.85f, dockC.z + 2.2f), 1.9f, 14);

            // Supermarket stairs to a little jetty, and the Home pier.
            Vector3 mTop = Deck("Supermarket", 1432, 600, 0.7f);
            Vector3 mBottom = P(1450, 660, 0.7f);
            StructureKit.Stairs(mb, mTop, mBottom, 1.7f, 15);
            StructureKit.Deck(mb, mBottom + new Vector3(0, 0, -1.2f), Quaternion.Euler(0, 10, 0), new Vector2(2.6f, 2.6f), 0f, 22);
            Vector3 pier = Rim("Home_LedgeRight", 410, 464, out var pierOut) + pierOut * 1.1f + Vector3.up * 0.05f;
            StructureKit.Deck(mb, pier, Quaternion.LookRotation(pierOut), new Vector2(2.4f, 2.4f), 0f, 23);

            MeshObject("Wood", g, mb, "Structures", Materials.Vertex, true);
        }

        static void BuildWaterfalls(Transform parent)
        {
            var g = Group("Waterfalls", parent);
            var sheet = new MeshBuilder();
            var splash = new MeshBuilder();
            var stream = new MeshBuilder();
            var falls = new (string id, float px, float py, float wpx)[]
            {
                ("Home", 280, 392, 38), ("School", 745, 268, 36), ("Park", 1213, 323, 20), ("Park", 1285, 320, 22),
                ("Hub", 655, 525, 14), ("Home_LedgeLeft", 200, 455, 16),
            };
            int seed = 0;
            foreach (var f in falls)
            {
                Vector3 rim = Rim(f.id, f.px, f.py, out var o);
                float width = U(f.px, f.py, rim.y) * f.wpx;
                KeepClear(rim, width * 0.6f + 0.6f);
                KeepClear(rim - o * 2.0f, width * 0.5f + 0.4f);
                Vector3 lip = rim + o * 0.32f + Vector3.up * 0.06f;
                StructureKit.WaterfallSheet(sheet, lip, o, width, -0.15f);
                float drop = lip.y;
                Vector3 basePt = lip + o * (Mathf.Min(1.4f, drop * 0.22f) + 0.5f);
                basePt.y = 0.05f;
                StructureKit.Splash(splash, basePt, width, 700 + seed++);
                // Stream feeding the fall across the grass.
                Vector3 side = Vector3.Cross(Vector3.up, o);
                Vector3 inner = rim - o * 2.6f;
                int a = stream.Add(inner - side * width * 0.35f + Vector3.up * 0.06f, Vector3.up, Color.white, new Vector2(0, 0));
                int b = stream.Add(inner + side * width * 0.35f + Vector3.up * 0.06f, Vector3.up, Color.white, new Vector2(1, 0));
                int c = stream.Add(lip + side * width * 0.5f, Vector3.up, Color.white, new Vector2(1, 0.06f));
                int d = stream.Add(lip - side * width * 0.5f, Vector3.up, Color.white, new Vector2(0, 0.06f));
                stream.QuadFacing(a, b, c, d, Vector3.up);
            }
            MeshObject("Sheets", g, sheet, "Water", Materials.Waterfall, false);
            MeshObject("Streams", g, stream, "Water", Materials.Waterfall, false);
            MeshObject("Splash", g, splash, "Water", Materials.Cloud, false);
        }

        // ------------------------------------------------------------------ blockout volumes

        static void BuildBlockoutLandmarks(Transform parent)
        {
            var g = Group("Blockout", parent);
            var mb = new MeshBuilder();
            void Building(float px, float py, float wpx, float hpxTop, Color wall, Color roof)
            {
                Vector3 b = G(px, py);
                float w = U(px, py, b.y) * wpx;
                float h = HeightFromPixels(px, py, hpxTop, b.y);
                float d = w * 0.6f;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(b + Vector3.up * h * 0.32f), new Vector3(w, h * 0.64f, d), 0.2f, 1, wall);
                Shapes.Extrude(mb, Matrix4x4.TRS(b + Vector3.up * h * 0.64f, Quaternion.Euler(0, 90, 0), Vector3.one), new List<Vector2>
                {
                    new Vector2(-d * 0.6f, 0), new Vector2(d * 0.6f, 0), new Vector2(0, h * 0.36f),
                }, w * 1.05f, roof);
            }
            void Figure(float px, float py, float hpx, Color c)
            {
                Vector3 b = G(px, py);
                float h = HeightFromPixels(px, py, py - hpx, b.y);
                Shapes.Ellipsoid(mb, Matrix4x4.Translate(b + Vector3.up * h * 0.5f), new Vector3(h * 0.3f, h * 0.5f, h * 0.3f), c);
            }
            void Sign(float cx, float cy, float wpx, float hpx, float groundPy)
            {
                Vector3 b = G(cx, groundPy);
                Vector3 c = PVertical(cx, cy, b);
                float w = U(cx, cy, c.y) * wpx;
                float h = PVertical(cx, cy - hpx * 0.5f, b).y - PVertical(cx, cy + hpx * 0.5f, b).y;
                Shapes.RoundedBox(mb, Matrix4x4.Translate(c), new Vector3(w, h, 0.4f), 0.2f, 1, StructureKit.WoodDark);
                Shapes.Box(mb, Matrix4x4.Translate(new Vector3(b.x - w * 0.3f, (b.y + c.y) * 0.5f, b.z)), new Vector3(0.35f, c.y - b.y, 0.35f), StructureKit.WoodDark);
                Shapes.Box(mb, Matrix4x4.Translate(new Vector3(b.x + w * 0.3f, (b.y + c.y) * 0.5f, b.z)), new Vector3(0.35f, c.y - b.y, 0.35f), StructureKit.WoodDark);
            }

            Building(385, 165, 170, 40, NumuColor.Hex("#f6e5c6"), NumuColor.Hex("#e0583c"));
            Building(185, 222, 100, 135, NumuColor.Hex("#f0c48c"), NumuColor.Hex("#d9503a"));
            Building(805, 152, 175, 20, NumuColor.Hex("#f6dcc0"), NumuColor.Hex("#e5574a"));
            Building(1330, 462, 270, 330, NumuColor.Hex("#f7e3b5"), NumuColor.Hex("#ef5a5a"));
            Building(775, 452, 110, 345, NumuColor.Hex("#f7ecd8"), NumuColor.Hex("#3aa58a"));
            Building(1125, 195, 140, 100, NumuColor.Hex("#e8503f"), NumuColor.Hex("#c8402f"));

            Figure(300, 258, 90, NumuColor.Hex("#ef8a2c"));
            Figure(727, 195, 75, NumuColor.Hex("#2f5fae"));
            Figure(1262, 140, 85, NumuColor.Hex("#fbe3ec"));
            Figure(1418, 470, 95, NumuColor.Hex("#c98a4a"));

            Sign(195, 155, 200, 60, 238);
            Sign(625, 50, 220, 60, 128);
            Sign(1125, 75, 205, 65, 138);
            Sign(1440, 325, 215, 65, 398);
            Sign(770, 490, 150, 60, 532);

            MeshObject("Massing", g, mb, "Blockout", Materials.Vertex, true);
        }
    }
}
