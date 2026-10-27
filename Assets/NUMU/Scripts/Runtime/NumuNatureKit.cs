using System.Collections.Generic;
using UnityEngine;

namespace Numu
{
    /// <summary>Stylised vegetation: puffy broadleaf trees, tiered pines, blossom trees, bushes and flowers.</summary>
    public static class NatureKit
    {
        public static readonly Color Bark = NumuColor.Hex("#8a5a34");
        public static readonly Color BarkLight = NumuColor.Hex("#a8743f");

        public struct LeafPalette
        {
            public Color Dark, Mid, Light, Tip;
            public LeafPalette(string dark, string mid, string light, string tip)
            {
                Dark = NumuColor.Hex(dark);
                Mid = NumuColor.Hex(mid);
                Light = NumuColor.Hex(light);
                Tip = NumuColor.Hex(tip);
            }
        }

        public static readonly LeafPalette Green = new LeafPalette("#2f8a3e", "#4fae45", "#86cc4f", "#c4e76a");
        public static readonly LeafPalette YellowGreen = new LeafPalette("#6a9e2c", "#9cc43a", "#c8dc4c", "#eef08a");
        public static readonly LeafPalette Pine = new LeafPalette("#1f6b3f", "#2f8c4a", "#4fae55", "#8fd06a");
        public static readonly LeafPalette Blossom = new LeafPalette("#c95596", "#ec7fb3", "#f9aacb", "#ffe0ee");
        public static readonly LeafPalette Hedge = new LeafPalette("#2f7f3a", "#46a046", "#74c24e", "#a9dd66");

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static System.Func<Vector3, Color> LeafShader(LeafPalette p, int seed)
        {
            return d =>
            {
                float n = NumuNoise.Value3(d.x * 2.2f + seed, d.y * 2.2f, d.z * 2.2f);
                float h = d.y * 0.55f + 0.45f + (n - 0.5f) * 0.35f;
                Color c = h < 0.45f ? Color.Lerp(p.Dark, p.Mid, h / 0.45f) : Color.Lerp(p.Mid, p.Light, (h - 0.45f) / 0.55f);
                if (h > 0.9f) c = Color.Lerp(c, p.Tip, (h - 0.9f) * 4f);
                return c;
            };
        }

        static void Trunk(MeshBuilder mb, Vector3 basePos, float height, float radius, int seed, bool branches = false)
        {
            var rng = new System.Random(seed);
            var path = new List<Vector3>();
            Vector3 lean = new Vector3(Rand(rng, -0.12f, 0.12f), 0, Rand(rng, -0.12f, 0.12f));
            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                path.Add(basePos + Vector3.up * (height * t - 0.25f) + lean * (t * t * height));
            }
            Shapes.Tube(mb, path, radius, 8, Bark, true, t => Mathf.Lerp(1.25f, 0.65f, t));
            // Root flare.
            Shapes.Blob(mb, Matrix4x4.Translate(basePos + Vector3.up * 0.02f), new Vector3(radius * 1.6f, radius * 0.6f, radius * 1.6f), Bark, 0.1f, 2f, seed, 1, 0.8f);
            if (!branches) return;
            for (int b = 0; b < 3; b++)
            {
                float a = b * 2.1f + Rand(rng, 0f, 0.6f);
                Vector3 start = basePos + Vector3.up * height * Rand(rng, 0.5f, 0.75f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 1.1f, Mathf.Sin(a)).normalized;
                Shapes.Tube(mb, new List<Vector3> { start, start + dir * height * 0.35f, start + dir * height * 0.55f + Vector3.up * 0.2f }, radius * 0.45f, 6, Bark, true, t => Mathf.Lerp(1f, 0.5f, t));
            }
        }

        /// <summary>Broadleaf tree with a lumpy, layered round canopy (the reference's most common tree).</summary>
        public static void RoundTree(MeshBuilder mb, Vector3 basePos, float height, int seed, LeafPalette? palette = null)
        {
            var rng = new System.Random(seed);
            var pal = palette ?? Green;
            int start = mb.VertexCount;
            float trunkH = height * 0.42f;
            Trunk(mb, basePos, trunkH + height * 0.15f, height * 0.06f, seed);
            float R = height * 0.36f;
            Vector3 c = basePos + Vector3.up * (trunkH + R * 0.85f);
            var shade = LeafShader(pal, seed);
            Shapes.Blob(mb, Matrix4x4.Translate(c), new Vector3(R, R * 0.92f, R), pal.Mid, 0.1f, 1.6f, seed, 3, 0.25f, shade);
            int lobes = 4 + rng.Next(3);
            for (int i = 0; i < lobes; i++)
            {
                float a = i / (float)lobes * Mathf.PI * 2f + Rand(rng, -0.3f, 0.3f);
                float r = R * Rand(rng, 0.5f, 0.68f);
                Vector3 p = c + new Vector3(Mathf.Cos(a) * R * 0.62f, Rand(rng, -0.35f, 0.15f) * R, Mathf.Sin(a) * R * 0.62f);
                Shapes.Blob(mb, Matrix4x4.Translate(p), new Vector3(r, r * 0.9f, r), pal.Mid, 0.12f, 1.8f, seed + i * 13, 2, 0.2f, shade);
            }
            Shapes.Blob(mb, Matrix4x4.Translate(c + Vector3.up * R * 0.6f), Vector3.one * R * 0.58f, pal.Light, 0.1f, 1.8f, seed + 77, 2, 0.2f, shade);
            mb.SetAlphaByHeight(basePos.y + trunkH * 0.8f, basePos.y + height, start);
        }

        /// <summary>Conifer built from stacked, scalloped tiers that droop at the rim.</summary>
        public static void PineTree(MeshBuilder mb, Vector3 basePos, float height, int seed, LeafPalette? palette = null)
        {
            var rng = new System.Random(seed);
            var pal = palette ?? Pine;
            int start = mb.VertexCount;
            Trunk(mb, basePos, height * 0.28f, height * 0.05f, seed);
            int tiers = 4;
            float baseR = height * 0.27f;
            float y = height * 0.18f;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);
                float r = baseR * Mathf.Lerp(1f, 0.42f, f);
                float h = height * Mathf.Lerp(0.36f, 0.3f, f);
                Tier(mb, basePos + Vector3.up * y, r, h, 9 + t % 2, rng.Next(999), pal, f);
                y += h * 0.52f;
            }
            mb.SetAlphaByHeight(basePos.y + height * 0.2f, basePos.y + height, start);
        }

        static void Tier(MeshBuilder mb, Vector3 basePos, float radius, float height, int lobes, int seed, LeafPalette pal, float tierF)
        {
            int seg = lobes * 6;
            int rings = 5;
            int start = mb.VertexCount, tStart = mb.Triangles.Count;
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                for (int i = 0; i < seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float wave = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * lobes * 0.5f)), 0.6f);
                    float rimR = radius * (0.86f + 0.14f * wave);
                    float r = Mathf.Lerp(rimR, 0.05f, Mathf.Pow(t, 0.85f));
                    float droop = (1f - t) * (1f - t) * (-0.18f * height) * (0.5f + 0.5f * wave);
                    float yy = t * height + droop - (j == 0 ? 0.12f * height : 0f);
                    if (j == 0) r *= 0.97f;
                    Vector3 p = basePos + new Vector3(Mathf.Cos(a) * r, yy, Mathf.Sin(a) * r);
                    float shade = Mathf.Lerp(0.15f, 1f, t) * (0.75f + 0.25f * wave) + tierF * 0.15f;
                    Color c = shade < 0.5f ? Color.Lerp(pal.Dark, pal.Mid, shade * 2f) : Color.Lerp(pal.Mid, pal.Light, (shade - 0.5f) * 2f);
                    mb.Add(p, Vector3.up, c);
                }
            }
            int apex = mb.Add(basePos + Vector3.up * height * 1.02f, Vector3.up, pal.Light);
            // Underside so tiers read as solid from below.
            int under = mb.Add(basePos + Vector3.up * (height * 0.05f), Vector3.down, pal.Dark);
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int i1 = (i + 1) % seg;
                    float a = (i + 0.5f) / seg * Mathf.PI * 2f;
                    Vector3 o = new Vector3(Mathf.Cos(a), 0.8f, Mathf.Sin(a));
                    mb.QuadFacing(start + j * seg + i, start + j * seg + i1, start + (j + 1) * seg + i1, start + (j + 1) * seg + i, o);
                }
            }
            for (int i = 0; i < seg; i++)
            {
                mb.TriFacing(apex, start + rings * seg + i, start + rings * seg + (i + 1) % seg, Vector3.up);
                mb.TriFacing(under, start + i, start + (i + 1) % seg, Vector3.down);
            }
            mb.RecalculateNormals(start, tStart);
        }

        /// <summary>Pink blossom tree: visible branching trunk and clustered pastel canopy.</summary>
        public static void BlossomTree(MeshBuilder mb, Vector3 basePos, float height, int seed)
        {
            var rng = new System.Random(seed);
            int start = mb.VertexCount;
            float trunkH = height * 0.45f;
            Trunk(mb, basePos, trunkH, height * 0.055f, seed, true);
            float R = height * 0.33f;
            Vector3 c = basePos + Vector3.up * (trunkH + R * 0.7f);
            var shade = LeafShader(Blossom, seed);
            int clusters = 6 + rng.Next(3);
            for (int i = 0; i < clusters; i++)
            {
                float a = i / (float)clusters * Mathf.PI * 2f + Rand(rng, -0.3f, 0.3f);
                float rr = R * Rand(rng, 0.5f, 0.7f);
                float dist = i == 0 ? 0f : R * Rand(rng, 0.45f, 0.75f);
                Vector3 p = c + new Vector3(Mathf.Cos(a) * dist, Rand(rng, -0.3f, 0.45f) * R, Mathf.Sin(a) * dist);
                Shapes.Blob(mb, Matrix4x4.Translate(p), new Vector3(rr * 1.1f, rr * 0.85f, rr * 1.1f), Blossom.Mid, 0.16f, 2.2f, seed + i * 7, 2, 0.3f, shade);
            }
            // White petal speckles.
            for (int i = 0; i < 14; i++)
            {
                Vector3 d = Random3(rng);
                d.y = Mathf.Abs(d.y);
                Shapes.Sphere(mb, c + Vector3.Scale(d, new Vector3(R * 1.15f, R * 0.95f, R * 1.15f)), R * 0.07f, NumuColor.Hex("#fff1f7"), 1);
            }
            mb.SetAlphaByHeight(basePos.y + trunkH * 0.8f, basePos.y + height, start);
        }

        static Vector3 Random3(System.Random rng)
        {
            Vector3 v;
            do v = new Vector3(Rand(rng, -1f, 1f), Rand(rng, -1f, 1f), Rand(rng, -1f, 1f));
            while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.05f);
            return v.normalized;
        }

        /// <summary>Rounded bush; optionally dotted with small flowers.</summary>
        public static void Bush(MeshBuilder mb, Vector3 basePos, float size, int seed, LeafPalette? palette = null, Color[] flowers = null)
        {
            var rng = new System.Random(seed);
            var pal = palette ?? Hedge;
            int start = mb.VertexCount;
            var shade = LeafShader(pal, seed);
            int lobes = 2 + rng.Next(3);
            var centers = new List<(Vector3 c, float r)>();
            for (int i = 0; i < lobes; i++)
            {
                float r = size * Rand(rng, 0.42f, 0.6f);
                Vector3 p = basePos + new Vector3(Rand(rng, -0.4f, 0.4f) * size, r * 0.75f, Rand(rng, -0.3f, 0.3f) * size);
                Shapes.Blob(mb, Matrix4x4.Translate(p), new Vector3(r * 1.1f, r, r), pal.Mid, 0.12f, 2f, seed + i * 11, 2, 0.45f, shade);
                centers.Add((p, r));
            }
            if (flowers != null && flowers.Length > 0)
            {
                for (int i = 0; i < Mathf.RoundToInt(size * 9f); i++)
                {
                    var (c, r) = centers[rng.Next(centers.Count)];
                    Vector3 d = Random3(rng);
                    d.y = Mathf.Abs(d.y) * 0.8f + 0.2f;
                    d.Normalize();
                    Shapes.Sphere(mb, c + d * r * 0.98f, size * 0.07f, flowers[rng.Next(flowers.Length)], 1);
                }
            }
            mb.SetAlphaByHeight(basePos.y, basePos.y + size * 1.4f, start);
            for (int i = start; i < mb.Colors.Count; i++) mb.Colors[i] = NumuColor.WithAlpha(mb.Colors[i], mb.Colors[i].a * 0.4f);
        }

        public static readonly Color[] FlowerColors =
        {
            NumuColor.Hex("#ff7aa8"), NumuColor.Hex("#ffffff"), NumuColor.Hex("#ffd84a"), NumuColor.Hex("#b48bf0"),
            NumuColor.Hex("#ff5b5b"), NumuColor.Hex("#ff9d3d"), NumuColor.Hex("#7fb8ff"),
        };

        /// <summary>A single little flower: stem, five rounded petals and a centre.</summary>
        public static void Flower(MeshBuilder mb, Vector3 basePos, float size, Color petal, int seed)
        {
            var rng = new System.Random(seed);
            float h = size * Rand(rng, 1.4f, 2.1f);
            Vector3 head = basePos + Vector3.up * h;
            Shapes.Beam(mb, basePos, head, size * 0.07f, 4, NumuColor.Hex("#3f9a3c"));
            Shapes.Ellipsoid(mb, Matrix4x4.TRS(basePos + Vector3.up * h * 0.35f + Vector3.right * size * 0.18f, Quaternion.Euler(0, rng.Next(360), 35), Vector3.one), new Vector3(size * 0.32f, size * 0.08f, size * 0.16f), NumuColor.Hex("#56b04a"), 1);
            Quaternion tilt = Quaternion.Euler(Rand(rng, -25f, 5f), rng.Next(360), 0);
            for (int i = 0; i < 5; i++)
            {
                Quaternion r = tilt * Quaternion.Euler(0, i * 72f, 0);
                Vector3 p = head + r * new Vector3(size * 0.32f, 0, 0);
                Shapes.Ellipsoid(mb, Matrix4x4.TRS(p, r, Vector3.one), new Vector3(size * 0.3f, size * 0.1f, size * 0.22f), petal, 1);
            }
            Shapes.Sphere(mb, head + tilt * Vector3.up * size * 0.06f, size * 0.15f, NumuColor.Hex("#ffd23f"), 1);
        }

        /// <summary>A loose patch of flowers and leaves scattered in a disc.</summary>
        public static void FlowerPatch(MeshBuilder mb, Vector3 center, float radius, int count, int seed, Color[] palette = null, System.Func<Vector3, Vector3> ground = null)
        {
            var rng = new System.Random(seed);
            palette ??= FlowerColors;
            int start = mb.VertexCount;
            for (int i = 0; i < count; i++)
            {
                float a = Rand(rng, 0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (ground != null) p = ground(p);
                Flower(mb, p, Rand(rng, 0.2f, 0.3f), palette[rng.Next(palette.Length)], rng.Next(99999));
            }
            mb.SetAlpha(0.15f, start);
        }

        /// <summary>Small clump of grass blades.</summary>
        public static void GrassTuft(MeshBuilder mb, Vector3 basePos, float size, int seed)
        {
            var rng = new System.Random(seed);
            int start = mb.VertexCount;
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * 360f + Rand(rng, -20, 20);
                Quaternion r = Quaternion.Euler(Rand(rng, 10f, 28f), a, 0);
                Shapes.Cone(mb, Matrix4x4.TRS(basePos, r, new Vector3(1f, 1f, 0.45f)), size * 0.12f, size * Rand(rng, 0.7f, 1.1f), 4, Color.Lerp(NumuColor.Hex("#4ea83f"), NumuColor.Hex("#93d25a"), (float)rng.NextDouble()));
            }
            mb.SetAlphaByHeight(basePos.y, basePos.y + size, start);
        }

        /// <summary>Smooth pebble / garden stone.</summary>
        public static void Stone(MeshBuilder mb, Vector3 basePos, float size, int seed, Color? color = null)
        {
            Shapes.Blob(mb, Matrix4x4.TRS(basePos + Vector3.up * size * 0.25f, Quaternion.Euler(0, seed * 41 % 360, 0), Vector3.one), new Vector3(size, size * 0.6f, size * 0.85f),
                color ?? NumuColor.Hex("#c9c3bd"), 0.15f, 1.6f, seed, 2, 0.5f);
        }
    }
}
