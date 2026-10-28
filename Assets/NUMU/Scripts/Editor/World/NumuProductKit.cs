using System.Collections.Generic;
using UnityEngine;

namespace Numu.EditorTools.World
{
    /// <summary>
    /// Supermarket stock: a generated packaging atlas (8×8 labels with printed product names) and low-poly
    /// boxes, cans, bottles and cartons UV-mapped onto it, plus colour-palette produce for fruit piles.
    /// Everything shares one material per family, so full shelves stay cheap to draw on mobile.
    /// </summary>
    public static class NumuProductKit
    {
        public enum Kind { Box, Can, Bottle, Carton, Jar }

        public struct Product
        {
            public Kind kind;
            public int cell;          // atlas cell 0..63
            public Vector3 size;      // width, height, depth (diameter for round items)
        }

        const int Cells = 8;

        // Row 0-2: boxed goods, 3-4: cans, 5-6: bottles and jars, 7: cartons and household.
        static readonly string[] BoxNames = { "CORN FLAKES", "OATS", "RICE", "PASTA", "COOKIES", "CRACKERS", "TEA", "FLOUR", "SUGAR", "COCOA", "GRANOLA", "BISCUITS",
            "NOODLES", "COUSCOUS", "LENTILS", "CHOCO PUFFS", "MUESLI", "HONEY O's", "WHEAT BITES", "DATES", "COFFEE", "BRAN", "WAFERS", "RAISINS" };
        static readonly string[] CanNames = { "BEANS", "SOUP", "SWEET CORN", "PEAS", "TUNA", "TOMATOES", "CHICKPEAS", "PEACHES", "CARROTS", "MUSHROOMS", "OLIVES", "LENTIL SOUP",
            "SARDINES", "FAVA BEANS", "CREAM", "PINEAPPLE" };
        static readonly string[] BottleNames = { "ORANGE", "APPLE", "MANGO", "WATER", "MILK", "LABAN", "SUN OIL", "VINEGAR", "KETCHUP", "HONEY", "JAM", "OLIVE OIL",
            "LEMONADE", "GUAVA", "TAHINI", "SAUCE" };
        static readonly string[] CartonNames = { "MILK", "JUICE", "YOGURT", "SOAP", "TISSUES", "DETERGENT", "", "" };
        static readonly string[] Palette = { "#d9412b", "#f2a51a", "#2f7fc1", "#3f9b4f", "#8a4fb3", "#e66b96", "#1f8a8a", "#c8562a", "#e8c33a", "#5d6d7e", "#a33a3a", "#3c5aa8", "#7fa83a", "#d47b2a", "#f4f1e8", "#2d3e50" };

        public static Texture2D BuildAtlas(string path)
        {
            const int size = 1024, cs = size / Cells;
            var c = new NumuSignKit.Canvas(size, size, Color.white);
            var rng = new System.Random(42);
            for (int i = 0; i < 64; i++)
            {
                int x = (i % Cells) * cs, y = (i / Cells) * cs;
                Color bg = NumuColor.Hex(Palette[(i * 7 + i / 8) % Palette.Length]);
                Color ink = bg.grayscale > 0.6f ? NumuColor.Hex("#2b2b33") : Color.white;
                Color accent = NumuColor.Hex(Palette[(i * 3 + 5) % Palette.Length]);
                int row = i / Cells;
                c.Rect(x, y, cs, cs, bg);
                if (row <= 2)
                {
                    string name = BoxNames[i];
                    c.Rect(x, y + cs - 30, cs, 30, NumuColor.Shade(bg, 0.75f));
                    c.Circle(x + 22, y + cs - 15, 11, accent);
                    c.Rect(x + 36, y + cs - 22, 70, 12, new Color(1, 1, 1, 0.8f), 3);
                    // A simple "product shot": a bowl or pile of pieces.
                    c.Ellipse(x + cs / 2f, y + 32, 40, 16, NumuColor.Hex("#f4f1e8"));
                    for (int k = 0; k < 9; k++) c.Circle(x + cs / 2f - 26 + (k % 5) * 13, y + 40 + (k / 5) * 8, 6, NumuColor.Hex(k % 2 == 0 ? "#e2a03f" : "#c47b25"));
                    c.Text(name, x + 6, y + 54, cs - 12, 40, ink);
                    c.Rect(x + 10, y + 8, cs - 20, 6, new Color(ink.r, ink.g, ink.b, 0.5f));
                }
                else if (row <= 4)
                {
                    string name = CanNames[i - 24];
                    c.Rect(x, y + 34, cs, 60, Color.white);
                    c.Rect(x, y + 30, cs, 4, accent);
                    c.Rect(x, y + 94, cs, 4, accent);
                    c.Text(name, x + 6, y + 44, cs - 12, 40, NumuColor.Shade(bg, 0.8f));
                    c.Circle(x + 20, y + 112, 8, accent);
                    c.Circle(x + cs - 20, y + 14, 8, accent);
                }
                else if (row <= 6)
                {
                    string name = BottleNames[i - 40];
                    c.Rect(x, y + 28, cs, 70, Color.Lerp(bg, Color.white, 0.15f));
                    c.Ellipse(x + cs / 2f, y + 63, 56, 30, Color.white);
                    c.Text(name, x + 14, y + 46, cs - 28, 34, NumuColor.Shade(bg, 0.7f));
                    c.Rect(x, y + 98, cs, 6, NumuColor.Shade(bg, 0.7f));
                }
                else
                {
                    int k = i - 56;
                    if (k < 6)
                    {
                        c.Rect(x, y + cs - 26, cs, 26, Color.white);
                        c.Text(CartonNames[k], x + 6, y + 40, cs - 12, 44, ink);
                        c.Rect(x + 12, y + 14, 40, 18, new Color(1, 1, 1, 0.7f), 4);
                    }
                    else if (k == 6) // brushed metal (can lids)
                    {
                        c.Rect(x, y, cs, cs, NumuColor.Hex("#b8bcc2"));
                        for (int r = 0; r < 12; r++) c.Rect(x, y + r * 11, cs, 2, NumuColor.Hex("#a6abb2"));
                    }
                    else // white plastic (caps, carton tops)
                        c.Rect(x, y, cs, cs, NumuColor.Hex("#f2f2ee"));
                }
                // Barcode on every label.
                for (int b = 0; b < 10; b++) c.Rect(x + cs - 30 + b * 2.4f, y + 4, (b * 7 % 3) + 1, 12, NumuColor.Hex("#222222"));
            }
            c.Grain(0.05f, 9);
            return c.Save(path, 1024);
        }

        static Rect CellRect(int cell)
        {
            float s = 1f / Cells, inset = 1.5f / 1024f;
            return new Rect((cell % Cells) * s + inset, (cell / Cells) * s + inset, s - inset * 2, s - inset * 2);
        }

        public const int MetalCell = 62, PlasticCell = 63;

        /// <summary>Random product of a family, sized like the real thing (metres).</summary>
        public static Product Random(System.Random rng, string family)
        {
            switch (family)
            {
                case "cans": return new Product { kind = Kind.Can, cell = 24 + rng.Next(16), size = new Vector3(0.075f, 0.11f, 0.075f) };
                case "bottles":
                    return rng.NextDouble() < 0.3
                        ? new Product { kind = Kind.Jar, cell = 40 + rng.Next(16), size = new Vector3(0.08f, 0.12f, 0.08f) }
                        : new Product { kind = Kind.Bottle, cell = 40 + rng.Next(16), size = new Vector3(0.08f, 0.26f + (float)rng.NextDouble() * 0.06f, 0.08f) };
                case "cartons": return new Product { kind = Kind.Carton, cell = 56 + rng.Next(3), size = new Vector3(0.07f, 0.2f, 0.07f) };
                case "household": return new Product { kind = rng.NextDouble() < 0.5 ? Kind.Bottle : Kind.Box, cell = 59 + rng.Next(3), size = new Vector3(0.1f, 0.24f, 0.07f) };
                default:
                    {
                        float w = 0.13f + (float)rng.NextDouble() * 0.09f;
                        return new Product { kind = Kind.Box, cell = rng.Next(24), size = new Vector3(w, w * (1.3f + (float)rng.NextDouble() * 0.4f), 0.06f + (float)rng.NextDouble() * 0.03f) };
                    }
            }
        }

        /// <summary>Adds one product standing on <paramref name="basePos"/>, its label facing <paramref name="facing"/>.</summary>
        public static void Add(MeshBuilder mb, Product p, Vector3 basePos, Quaternion facing)
        {
            var part = new MeshBuilder();
            var r = CellRect(p.cell);
            switch (p.kind)
            {
                case Kind.Box: BoxMesh(part, p.size, r); break;
                case Kind.Carton: CartonMesh(part, p.size, r); break;
                case Kind.Can: Round(part, new[] { new Vector2(0.5f, 0), new Vector2(0.5f, 1) }, p.size, 8, r, CellRect(MetalCell), 0f, 1f); break;
                case Kind.Jar: Round(part, new[] { new Vector2(0.5f, 0), new Vector2(0.5f, 0.82f), new Vector2(0.42f, 0.86f), new Vector2(0.42f, 1f) }, p.size, 8, r, CellRect(PlasticCell), 0f, 0.82f); break;
                default:
                    Round(part, new[] { new Vector2(0.5f, 0), new Vector2(0.5f, 0.62f), new Vector2(0.38f, 0.76f), new Vector2(0.16f, 0.86f), new Vector2(0.16f, 1f) }, p.size, 7, r, CellRect(PlasticCell), 0.12f, 0.62f);
                    break;
            }
            mb.Append(part, Matrix4x4.TRS(basePos, facing, Vector3.one));
        }

        static void BoxMesh(MeshBuilder mb, Vector3 s, Rect r)
        {
            // Front and back carry the full label; sides and top take a thin strip of it (colour continuity).
            Vector3 h = s * 0.5f;
            Rect strip = new Rect(r.x, r.y + r.height * 0.3f, r.width * 0.12f, r.height * 0.5f);
            // Seen from the front, +x is on the viewer's left: run u towards -x so printed text reads correctly.
            Face(mb, new Vector3(h.x, 0, h.z), Vector3.left * s.x, Vector3.up * s.y, Vector3.forward, r);
            Face(mb, new Vector3(-h.x, 0, -h.z), Vector3.right * s.x, Vector3.up * s.y, Vector3.back, r);
            Face(mb, new Vector3(h.x, 0, h.z), Vector3.back * s.z, Vector3.up * s.y, Vector3.right, strip);
            Face(mb, new Vector3(-h.x, 0, -h.z), Vector3.forward * s.z, Vector3.up * s.y, Vector3.left, strip);
            Face(mb, new Vector3(-h.x, s.y, h.z), Vector3.right * s.x, Vector3.back * s.z, Vector3.up, strip);
        }

        static void CartonMesh(MeshBuilder mb, Vector3 s, Rect r)
        {
            float body = s.y * 0.82f;
            BoxMesh(mb, new Vector3(s.x, body, s.z), r);
            // Gable top.
            Vector3 h = s * 0.5f;
            var top = CellRect(PlasticCell);
            Face(mb, new Vector3(-h.x, body, h.z), Vector3.right * s.x, new Vector3(0, s.y - body, -h.z), new Vector3(0, h.z, s.y - body).normalized, top);
            Face(mb, new Vector3(h.x, body, -h.z), Vector3.left * s.x, new Vector3(0, s.y - body, h.z), new Vector3(0, h.z, -(s.y - body)).normalized, top);
        }

        static void Face(MeshBuilder mb, Vector3 o, Vector3 u, Vector3 v, Vector3 n, Rect r)
        {
            int a = mb.Add(o, n, Color.white, new Vector2(r.xMin, r.yMin));
            int b = mb.Add(o + u, n, Color.white, new Vector2(r.xMax, r.yMin));
            int c = mb.Add(o + u + v, n, Color.white, new Vector2(r.xMax, r.yMax));
            int d = mb.Add(o + v, n, Color.white, new Vector2(r.xMin, r.yMax));
            mb.QuadFacing(a, b, c, d, n);
        }

        /// <summary>Lathed round product. Profile x is the radius fraction of the diameter, y the height fraction; the label spans labelFrom..labelTo.</summary>
        static void Round(MeshBuilder mb, Vector2[] profile, Vector3 s, int seg, Rect label, Rect cap, float labelFrom, float labelTo)
        {
            float rad = s.x, hgt = s.y;
            for (int k = 0; k < profile.Length - 1; k++)
            {
                Vector2 p0 = profile[k], p1 = profile[k + 1];
                bool onLabel = p0.y >= labelFrom - 1e-4f && p1.y <= labelTo + 1e-4f && p1.y > p0.y;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                    Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                    Vector3 q00 = d0 * p0.x * rad + Vector3.up * p0.y * hgt, q10 = d1 * p0.x * rad + Vector3.up * p0.y * hgt;
                    Vector3 q01 = d0 * p1.x * rad + Vector3.up * p1.y * hgt, q11 = d1 * p1.x * rad + Vector3.up * p1.y * hgt;
                    Rect rr = onLabel ? label : cap;
                    // The label wraps half the circumference twice, so the name reads from the front
                    // (u runs against the angle, otherwise the print would be mirrored).
                    float u1 = 1f - Mathf.Repeat(i / (float)seg * 2f, 1f), u0 = u1 + 2f / seg;
                    if (u0 > 1f) { u0 -= 2f / seg; u1 -= 2f / seg; }
                    float v0 = onLabel ? (p0.y - labelFrom) / (labelTo - labelFrom) : 0.2f, v1 = onLabel ? (p1.y - labelFrom) / (labelTo - labelFrom) : 0.8f;
                    Vector3 n0 = (d0 + Vector3.up * (p0.x - p1.x) * rad / Mathf.Max(1e-4f, (p1.y - p0.y) * hgt)).normalized;
                    Vector3 n1 = (d1 + Vector3.up * (p0.x - p1.x) * rad / Mathf.Max(1e-4f, (p1.y - p0.y) * hgt)).normalized;
                    int a = mb.Add(q00, n0, Color.white, new Vector2(Mathf.Lerp(rr.xMin, rr.xMax, u0), Mathf.Lerp(rr.yMin, rr.yMax, v0)));
                    int b = mb.Add(q10, n1, Color.white, new Vector2(Mathf.Lerp(rr.xMin, rr.xMax, Mathf.Min(u1, 1f)), Mathf.Lerp(rr.yMin, rr.yMax, v0)));
                    int c = mb.Add(q11, n1, Color.white, new Vector2(Mathf.Lerp(rr.xMin, rr.xMax, Mathf.Min(u1, 1f)), Mathf.Lerp(rr.yMin, rr.yMax, v1)));
                    int d = mb.Add(q01, n0, Color.white, new Vector2(Mathf.Lerp(rr.xMin, rr.xMax, u0), Mathf.Lerp(rr.yMin, rr.yMax, v1)));
                    mb.QuadFacing(a, b, c, d, (d0 + d1).normalized);
                }
            }
            // Top cap.
            Vector2 top = profile[profile.Length - 1];
            int centre = mb.Add(Vector3.up * top.y * hgt, Vector3.up, Color.white, cap.center);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                int a = mb.Add(new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * top.x * rad + Vector3.up * top.y * hgt, Vector3.up, Color.white, new Vector2(cap.xMin, cap.yMin));
                int b = mb.Add(new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * top.x * rad + Vector3.up * top.y * hgt, Vector3.up, Color.white, new Vector2(cap.xMax, cap.yMin));
                mb.TriFacing(centre, a, b, Vector3.up);
            }
        }

        /// <summary>
        /// Stocks one shelf: blocks of identical facings (as in real stores), front-aligned, along +x of the frame.
        /// <paramref name="frame"/> maps (x along shelf, y up, z towards the aisle).
        /// </summary>
        public static int FillShelf(MeshBuilder mb, Matrix4x4 frame, float length, float depth, float clearance, string family, System.Random rng, float fill = 0.93f, int rows = 2)
        {
            int count = 0;
            float x = 0.02f;
            var face = frame.rotation;
            while (x < length - 0.05f)
            {
                var p = Random(rng, family);
                if (p.size.y > clearance - 0.02f) p.size *= (clearance - 0.03f) / p.size.y;
                int facings = 2 + rng.Next(5);
                bool gap = rng.NextDouble() > fill;
                for (int f = 0; f < facings && x + p.size.x < length; f++)
                {
                    if (!gap)
                    {
                        // Two rows deep (by default) so the shelf reads as full from an angle.
                        for (int row = 0; row < rows; row++)
                        {
                            float z = depth * 0.5f - p.size.z * 0.5f - 0.015f - row * (p.size.z + 0.01f);
                            if (z - p.size.z * 0.5f < -depth * 0.5f) break;
                            Vector3 local = new Vector3(x + p.size.x * 0.5f, 0, z);
                            Add(mb, p, frame.MultiplyPoint3x4(local), face * Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 4f, 0));
                            count++;
                        }
                    }
                    x += p.size.x + 0.006f;
                }
                x += 0.01f;
            }
            return count;
        }

        // ------------------------------------------------------------------ produce

        static readonly string[][] ProduceShades =
        {
            new[] { "#b3241d", "#c8352a", "#9f1f1a", "#d0452f", "#a82a22", "#bd3a25", "#c22f22", "#8f1c18" }, // red apples
            new[] { "#9bc23c", "#a8cc45", "#8fb534", "#b4d150", "#93bb3a", "#a2c742", "#87ad30", "#b0cf4a" }, // green apples
            new[] { "#f0a21b", "#f5ad25", "#e8961a", "#f7b733", "#eb9c1c", "#f2a82a", "#e48f17", "#f9bd3c" }, // oranges
            new[] { "#f2d43a", "#f5da45", "#ebcc32", "#f7e052", "#efd23c", "#f3d847", "#e8c92f", "#f8e35c" }, // lemons
            new[] { "#d93a2a", "#e04a32", "#c93024", "#e5553a", "#d2402c", "#de4734", "#c42d22", "#e85f42" }, // tomatoes
            new[] { "#b88a52", "#c0935a", "#ab7f4a", "#c69d63", "#b48550", "#bd9058", "#a87a46", "#cba468" }, // potatoes
            new[] { "#c27a3a", "#ca8442", "#b87034", "#d08c4a", "#c47c3c", "#cc8646", "#b36c32", "#d4924f" }, // onions
            new[] { "#4a7a2a", "#548532", "#426e25", "#5e8f38", "#4c7c2c", "#568734", "#3f6a23", "#62943c" }, // limes / peppers
        };

        public static Texture2D BuildProducePalette(string path)
        {
            var c = new NumuSignKit.Canvas(64, 64, Color.white);
            for (int r = 0; r < 8; r++)
                for (int k = 0; k < 8; k++)
                    c.Rect(k * 8, r * 8, 8, 8, NumuColor.Hex(ProduceShades[r][k]));
            return c.Save(path, 64);
        }

        /// <summary>A heap of round produce (one palette row) inside a crate-sized area, mounded towards the middle.</summary>
        public static void Pile(MeshBuilder mb, Vector3 center, Vector2 area, float height, int type, float radius, System.Random rng, float squash = 0.92f)
        {
            int nx = Mathf.Max(1, Mathf.FloorToInt(area.x / (radius * 1.9f))), nz = Mathf.Max(1, Mathf.FloorToInt(area.y / (radius * 1.9f)));
            int layers = Mathf.Max(1, Mathf.RoundToInt(height / (radius * 1.6f)));
            for (int l = 0; l < layers; l++)
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float u = (i + 0.5f) / nx - 0.5f, v = (j + 0.5f) / nz - 0.5f;
                        // Upper layers only near the middle: a natural mound.
                        if (l > 0 && Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) > 0.5f - l * 0.14f) continue;
                        Vector3 p = center + new Vector3(u * area.x + (float)(rng.NextDouble() - 0.5) * radius * 0.5f, radius * squash + l * radius * 1.45f, v * area.y + (float)(rng.NextDouble() - 0.5) * radius * 0.5f);
                        var part = new MeshBuilder();
                        float rr = radius * (0.9f + (float)rng.NextDouble() * 0.2f);
                        Shapes.Ellipsoid(part, Matrix4x4.TRS(p, Quaternion.Euler(rng.Next(360), rng.Next(360), 0), Vector3.one), new Vector3(rr, rr * squash, rr), Color.white, 1);
                        int shade = rng.Next(8);
                        Vector2 uv = new Vector2((shade + 0.5f) / 8f, (type + 0.5f) / 8f);
                        for (int k = 0; k < part.UVs.Count; k++) part.UVs[k] = uv;
                        mb.Append(part);
                    }
        }
    }
}
